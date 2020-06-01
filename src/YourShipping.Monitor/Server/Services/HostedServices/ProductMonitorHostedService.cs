namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;

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

    using Product = YourShipping.Monitor.Server.Models.Product;

    public class ProductMonitorHostedService : TimedHostedServiceBase
    {
        public ProductMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<User, int> userRepository,
            IRepository<Product, int> productRepository,
            IEntityScrapper<Product> productScrapper,
            IHubContext<MessagesHub> messageHubContext,
            ITelegramBotClient telegramBotClient = null)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Products);

            var sourceChanged = false;

            var storedProducts = productRepository.All().ToList();
            foreach (var storedProduct in storedProducts)
            {
                if (storedProduct.IsEnabled)
                {
                    var dateTime = DateTime.Now;
                    var product = await productScrapper.GetAsync(storedProduct.Url);
                    IDbContextTransaction transaction = null;
                    Log.Information("Updating scrapped product '{url}'", storedProduct.Url);
                    if (product == null)
                    {
                        product = storedProduct;
                        if (product.IsAvailable)
                        {
                            transaction = PolicyHelper.WaitAndRetry().Execute(
                                () => productRepository.BeginTransaction(IsolationLevel.Serializable));

                            product.IsAvailable = false;
                            product.Updated = dateTime;
                            product.Sha256 = JsonSerializer.Serialize(storedProduct).ComputeSHA256();
                            sourceChanged = true;

                            Log.Information(
                                "Product {Product} from {Store} has changed. Is Available: {IsAvailable}",
                                storedProduct.Name,
                                storedProduct.Store,
                                storedProduct.IsAvailable);
                        }
                    }
                    else if (product.Sha256 != storedProduct.Sha256)
                    {
                        transaction = PolicyHelper.WaitAndRetry().Execute(
                            () => productRepository.BeginTransaction(IsolationLevel.Serializable));

                        product.Id = storedProduct.Id;
                        product.Updated = dateTime;
                        productRepository.TryAddOrUpdate(product, nameof(Product.Added), nameof(Product.Read));
                        sourceChanged = true;

                        Log.Information(
                            "Product {Product} from {Store} has changed. Is Available: {IsAvailable}",
                            product.Name,
                            product.Store,
                            product.IsAvailable);
                    }

                    if (transaction != null)
                    {
                        await productRepository.SaveChangesAsync();
                        await transaction.CommitAsync();

                        var productDataTransferObject = product.ToDataTransferObject(true);
                        var message = JsonSerializer.Serialize(productDataTransferObject);
                        await messageHubContext.Clients.All.SendAsync(
                            ClientMethods.EntityChanged,
                            AlertSource.Products,
                            message);

                        Log.Information("Entity changed at source {Source}.", AlertSource.Products);

                        if (telegramBotClient != null)
                        {
                            var messageStringBuilder = new StringBuilder();
                            messageStringBuilder.AppendLine("*Product Changed*");
                            messageStringBuilder.AppendLine($"*Name:* _{productDataTransferObject.Name}_");
                            messageStringBuilder.AppendLine(
                                $"*Price:* _{productDataTransferObject.Price.ToString("C")} {productDataTransferObject.Currency}_");
                            messageStringBuilder.AppendLine(
                                $"*Is IsAvailable:* _{productDataTransferObject.IsAvailable}_");
                            if (productDataTransferObject.IsAvailable)
                            {
                                messageStringBuilder.AppendLine(
                                    $"*Link:* [{productDataTransferObject.Url}]({productDataTransferObject.Url})");
                            }

                            messageStringBuilder.AppendLine($"*Store:* _{productDataTransferObject.Store}_");
                            messageStringBuilder.AppendLine($"*Department:* _{productDataTransferObject.Department}_");
                            messageStringBuilder.AppendLine(
                                $"*Category:* _{productDataTransferObject.DepartmentCategory}_");

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
                    }
                }
            }

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Products);
        }
    }
}