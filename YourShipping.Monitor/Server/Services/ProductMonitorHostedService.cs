namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Threading.Tasks;

    using BlazorApp6.Server;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Product = YourShipping.Monitor.Server.Models.Product;

    public class ProductMonitorHostedService : TimedHostedServiceBase
    {
        private readonly ILogger<ProductMonitorHostedService> logger;

        public ProductMonitorHostedService(
            ILogger<ProductMonitorHostedService> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            this.logger = logger;
        }

        [Execute]
        public async Task Execute(
            IRepository<Product, int> productRepository,
            IEntityScrapper<Product> productScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            this.Logger.LogInformation("Running Products Monitor.");
            bool domainChanged = false;
            foreach (var storedProduct in productRepository.All())
            {
                var dateTime = DateTime.Now;
                var product = await productScrapper.GetAsync(storedProduct.Url);
                if (product != null)
                {
                    var hasChanged = product.Name != storedProduct.Name
                                     || Math.Abs(product.Price - storedProduct.Price) > 0.001
                                     || product.Store != storedProduct.Store
                                     || product.Currency != storedProduct.Currency
                                     || product.IsAvailable != storedProduct.IsAvailable;

                    if (hasChanged)
                    {
                        if (!domainChanged)
                        {
                            domainChanged = product.IsAvailable;
                        }

                        product.Id = storedProduct.Id;
                        product.Updated = dateTime;
                        productRepository.TryAddOrUpdate(product, nameof(Product.Added), nameof(Product.Read));
                    }
                }
            }

            await productRepository.SaveChangesAsync();

            if (domainChanged)
            {
                this.Logger.LogInformation("Products change detected");
                await messageHubContext.Clients.All.SendAsync("DomainChanged", AlertSource.Products);
            }
            else
            {
                this.Logger.LogInformation("No Products change detected");
            }
        }
    }
}