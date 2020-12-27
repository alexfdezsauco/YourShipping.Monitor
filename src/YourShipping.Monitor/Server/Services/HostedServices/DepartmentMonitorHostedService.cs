namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Collections.Immutable;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Dasync.Collections;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;
    using Telegram.Bot.Types;
    using Telegram.Bot.Types.Enums;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;
    using File = System.IO.File;
    using Product = YourShipping.Monitor.Server.Models.Product;
    using User = YourShipping.Monitor.Server.Models.User;

    public sealed class DepartmentMonitorHostedService : TimedHostedServiceBase
    {
        public DepartmentMonitorHostedService(IServiceProvider serviceProvider, IConfiguration configuration)
            : base(
                serviceProvider,
                TimeSpan.FromSeconds(1),
                bool.TryParse(configuration["MaximizeParallelism"], out var maximizeParallelism) && maximizeParallelism)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Department, int> globalDepartmentRepository,
            IRepository<Product, int> globalProductRepository,
            IServiceProvider serviceProvider,
            IHubContext<MessagesHub> messageHubContext,
            ITelegramBotClient telegramBotClient = null)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Departments);

            var sourceChanged = false;

            // TODO: Improve this.
            var storedDepartments = globalDepartmentRepository.Find(s => s.IsEnabled).ToList();
            var disabledProducts = globalProductRepository.Find(p => !p.IsEnabled).Select(p => p.Url)
                .ToImmutableSortedSet();

            await storedDepartments.ParallelForEachAsync(
                async storedDepartment =>
                    {
                        var serviceScope = serviceProvider.CreateScope();
                        var serviceScopeServiceProvider = serviceScope.ServiceProvider;
                        var departmentRepository =
                            serviceScopeServiceProvider.GetService<IRepository<Department, int>>();
                        var userRepository = serviceScopeServiceProvider.GetService<IRepository<User, int>>();
                        var departmentScrapper = serviceProvider.GetService<IEntityScraper<Department>>();

                        // var storedDepartmentStore = storedDepartment.Store;
                        // var storedDepartmentName = storedDepartment.Name;
                        // var disabledProducts = productRepository.Find(p => p.Store == storedDepartmentStore && p.Department == storedDepartmentName && !p.IsEnabled)
                        // .Select(p => p.Url).ToImmutableSortedSet();
                        var dateTime = DateTime.Now;
                        var department = await departmentScrapper.GetAsync(
                                             storedDepartment.Url,
                                             true,
                                             disabledProducts);
                        IDbContextTransaction transaction = null;
                        Log.Information("Updating scrapped department '{url}'", storedDepartment.Url);
                        if (department == null)
                        {
                            department = storedDepartment;
                            if (department.IsAvailable)
                            {
                                transaction = PolicyHelper.WaitAndRetry().Execute(
                                    () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));

                                department.IsAvailable = false;
                                department.Updated = dateTime;
                                department.Sha256 = JsonSerializer.Serialize(department).ComputeSha256();

                                sourceChanged = true;
                                Log.Information(
                                    "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                                    department.Name,
                                    department.Store,
                                    department.IsAvailable);
                            }
                        }
                        else if (department.Sha256 != storedDepartment.Sha256)
                        {
                            transaction = PolicyHelper.WaitAndRetry().Execute(
                                () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));

                            department.Id = storedDepartment.Id;
                            department.Updated = dateTime;
                            departmentRepository.TryAddOrUpdate(
                                department,
                                nameof(Department.Added),
                                nameof(Department.Read));
                            sourceChanged = true;

                            Log.Information(
                                "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                                department.Name,
                                department.Store,
                                department.IsAvailable);
                        }

                        if (transaction != null)
                        {
                            await departmentRepository.SaveChangesAsync();
                            await transaction.CommitAsync();

                            // var repository = serviceProvider.GetService<IRepository<Product, int>>();
                            // foreach (var departmentProduct in department.Products.Values)
                            // {
                            // var departmentProductUrl = departmentProduct.Url;
                            // repository.TryAddOrUpdate(departmentProduct);
                            // }
                            var departmentDataTransferObject = department.ToDataTransferObject(true);
                            var message = JsonSerializer.Serialize(departmentDataTransferObject);
                            await messageHubContext.Clients.All.SendAsync(
                                ClientMethods.EntityChanged,
                                AlertSource.Departments,
                                message);

                            using var httpClient = new HttpClient();
                            foreach (var departmentProduct in department.Products.Values)
                            {
                                var imageStream = await httpClient.GetStreamAsync(departmentProduct.ImageUrl);

                                var storeSlug = UriHelper.GetStoreSlug(department.Url);
                                if (!Directory.Exists($"logs/products/{storeSlug}"))
                                {
                                    Directory.CreateDirectory($"logs/products/{storeSlug}");
                                }

                                var baseFilePath = $"logs/products/{storeSlug}/{departmentProduct.Name.ComputeSha256()}";

                                var imageFilePath = $"{baseFilePath}.jpg";
                                try
                                {
                                    await using var fileStream = File.Create(imageFilePath);
                                    await imageStream.CopyToAsync(fileStream);
                                    await fileStream.FlushAsync();
                                }
                                catch (Exception e)
                                {
                                    Log.Warning(e, "Error saving image.");
                                }

                                var textFilePath = $"{baseFilePath}.txt";
                                try
                                {
                                    var builder = new StringBuilder();
                                    builder.AppendLine(departmentProduct.Name);
                                    builder.AppendLine($"{departmentProduct.Price} {departmentProduct.Currency}");
                                    await File.WriteAllTextAsync(textFilePath, builder.ToString());
                                }
                                catch (Exception e)
                                {
                                    Log.Warning(e, "Error saving description");
                                }
                            }

                            if (telegramBotClient != null && department.IsAvailable)
                            {
                                var messageStringBuilder = new StringBuilder();
                                messageStringBuilder.AppendLine("*Product Set Changed*");
                                messageStringBuilder.AppendLine($"*Name:* _{departmentDataTransferObject.Name}_");
                                messageStringBuilder.AppendLine(
                                    $"*Category:* _{departmentDataTransferObject.Category}_");
                                messageStringBuilder.AppendLine(
                                    $"*Products Count:* _{departmentDataTransferObject.ProductsCount}_");
                                messageStringBuilder.AppendLine(
                                    $"*Is Available:* _{departmentDataTransferObject.IsAvailable}_");

                                if (departmentDataTransferObject.ProductsCount > 0)
                                {
                                    messageStringBuilder.AppendLine(
                                        $"*Link:* [{departmentDataTransferObject.Url}]({departmentDataTransferObject.Url})");
                                }

                                // TODO: Use the DTO instead.
                                foreach (var keyValuePair in department.Products)
                                {
                                    var product = keyValuePair.Value;
                                    messageStringBuilder.AppendLine("------------------------------");
                                    messageStringBuilder.AppendLine($"*Product Name:* {product.Name}");
                                    messageStringBuilder.AppendLine(
                                        $"*Product Price:* _{product.Price:C} {product.Currency}_");
                                    messageStringBuilder.AppendLine($"*Product Is Available:* {product.IsAvailable}");
                                    messageStringBuilder.AppendLine($"*Product Is In Cart:* {product.IsInCart}");
                                    if (product.IsAvailable)
                                    {
                                        // TODO: Improve this to send the image.
                                        messageStringBuilder.AppendLine(
                                            $"*Product Link:* [{product.Url}]({product.Url})");
                                        messageStringBuilder.AppendLine(
                                            $"*Product Image:* [{product.ImageUrl}]({product.ImageUrl})");
                                    }

                                    messageStringBuilder.AppendLine("------------------------------");
                                }

                                messageStringBuilder.AppendLine($"*Store:* _{departmentDataTransferObject.Store}_");
                                var markdownMessage = messageStringBuilder.ToString();

                                var users = userRepository.Find(user => user.IsEnable).ToList();
                                foreach (var user in users)
                                {
                                    try
                                    {
                                        await telegramBotClient.SendTextMessageAsync(
                                            user.ChatId,
                                            markdownMessage,
                                            ParseMode.Markdown);
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Warning(
                                            e,
                                            "Error sending notification messages via telegram to {UserName}",
                                            user.Name);
                                    }

                                    using var client = new HttpClient();
                                    foreach (var departmentProduct in department.Products.Values)
                                    {
                                        var storeSlug = UriHelper.GetStoreSlug(department.Url);
                                        var baseFilePath = $"logs/products/{storeSlug}/{departmentProduct.Name.ComputeSha256()}";
                                        var imageFilePath = $"{baseFilePath}.jpg";
                                        if (File.Exists(imageFilePath))
                                        {
                                            try
                                            {
                                                await telegramBotClient.SendPhotoAsync(
                                                    user.ChatId,
                                                    new InputMedia(
                                                        new FileStream(imageFilePath, FileMode.Open),
                                                        "photo.jpg"),
                                                    departmentProduct.Name);
                                            }
                                            catch (Exception e)
                                            {
                                                Log.Warning(
                                                    e,
                                                    "Error sending detailed messages via telegram to {UserName}",
                                                    user.Name);
                                            }
                                        }
                                    }
                                }
                            }

                            Log.Information("Entity changed at source {Source}.", AlertSource.Departments);
                        }
                        else
                        {
                            Log.Information("No change detected for department '{url}'", storedDepartment.Url);
                        }
                    });

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Departments);
        }
    }
}