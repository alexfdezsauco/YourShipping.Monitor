namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;

    using YourShipping.Monitor.Server.Extensions;
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
            IUnitOfWork unitOfWork,
            IEntityScrapper<Product> productScrapper,
            IHubContext<MessagesHub> messageHubContext,
            ITelegramBotClient telegramBotClient = null)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Products);

            var sourceChanged = false;

            var productRepository = unitOfWork.GetRepository<Product, int>();
            foreach (var storedProduct in productRepository.All())
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
                        transaction = productRepository.BeginTransaction(IsolationLevel.Serializable);
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
                    transaction = productRepository.BeginTransaction(IsolationLevel.Serializable);
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

                    var message = JsonSerializer.Serialize(product.ToDataTransferObject(true));
                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Products,
                        message);

                    Log.Information("Entity changed at source {Source}.", AlertSource.Products);

                    var userRepository = unitOfWork.GetRepository<User, int>();
                    var users = userRepository.Find(user => user.IsEnable).ToList();
                    foreach (var user in users)
                    {
                        try
                        {
                            await telegramBotClient.SendTextMessageAsync(user.ChatId, "Product Changed: " + message);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error sending message via telegram to {UserName}", user.Name);
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