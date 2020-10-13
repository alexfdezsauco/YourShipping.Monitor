namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Collections.Immutable;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Dasync.Collections;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.DependencyInjection;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;
    using Telegram.Bot.Types.Enums;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;
    using Product = YourShipping.Monitor.Server.Models.Product;

    public sealed class DepartmentMonitorHostedService : TimedHostedServiceBase
    {
        public DepartmentMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider, TimeSpan.FromSeconds(1))
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

                            //var repository = serviceProvider.GetService<IRepository<Product, int>>();
                            //foreach (var departmentProduct in department.Products.Values)
                            //{
                            //    var departmentProductUrl = departmentProduct.Url;
                            //    repository.TryAddOrUpdate(departmentProduct);
                            //}

                            var departmentDataTransferObject = department.ToDataTransferObject(true);
                            var message = JsonSerializer.Serialize(departmentDataTransferObject);
                            await messageHubContext.Clients.All.SendAsync(
                                ClientMethods.EntityChanged,
                                AlertSource.Departments,
                                message);

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
                                        messageStringBuilder.AppendLine(
                                            $"*Product Link:* [{product.Url}]({product.Url})");
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
                                        Log.Error(e, "Error sending message via telegram to {UserName}", user.Name);
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