namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Product = YourShipping.Monitor.Server.Models.Product;

    public class ProductMonitorHostedService : TimedHostedServiceBase
    {

        public ProductMonitorHostedService(
            IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Product, int> productRepository,
            IEntityScrapper<Product> productScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Products);

            var sourceChanged = false;
            foreach (var storedProduct in productRepository.All())
            {
                var dateTime = DateTime.Now;
                var product = await productScrapper.GetAsync(storedProduct.Url);
                if (product != null)
                {
                    if (product.Sha256 != storedProduct.Sha256)
                    {
                        if (!sourceChanged)
                        {
                            sourceChanged = product.IsAvailable;
                        }

                        product.Id = storedProduct.Id;
                        product.Updated = dateTime;
                        productRepository.TryAddOrUpdate(product, nameof(Product.Added), nameof(Product.Read));
                    }
                }
            }

            await productRepository.SaveChangesAsync();

            if (sourceChanged)
            {
                Log.Information("{Source} change detected", AlertSource.Products);
                await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Products);
            }
            else
            {
                Log.Information("No {Source} change detected", AlertSource.Products);
            }
        }
    }
}