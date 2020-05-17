namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> logger;

        public ProductsController(ILogger<ProductsController> logger)
        {
            this.logger = logger;
        }

        [HttpPost]
        public async Task<Shared.Product> Add(
            [FromServices] IRepository<Product, int> productRepository,
            [FromServices] IEntityScrapper<Product> entityScrapper,
            [FromBody] Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var storedProduct = productRepository.Find(product => product.Url == absoluteUrl).FirstOrDefault();
            if (storedProduct == null)
            {
                var dateTime = DateTime.Now;
                var product = await entityScrapper.GetAsync(absoluteUrl);
                if (product != null)
                {
                    product.Added = dateTime;
                    product.Updated = dateTime;
                    product.Read = dateTime;

                    productRepository.Add(product);
                    await productRepository.SaveChangesAsync();

                    return new Shared.Product
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Price = product.Price,
                        Url = product.Url,
                        Currency = product.Currency,
                        Store = product.Store,
                        IsAvailable = product.IsAvailable
                    };
                }
            }

            return null;
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Product, int> productRepository, int id)
        {
            productRepository.Delete(product => product.Id == id);
            await productRepository.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IEnumerable<Shared.Product>> Get(
            [FromServices] IRepository<Product, int> productRepository,
            [FromServices] IEntityScrapper<Product> entityScrapper)
        {
            var products = new List<Shared.Product>();

            foreach (var storedProduct in productRepository.All())
            {
                var dateTime = DateTime.Now;
                Product product;
                var hasChanged = storedProduct.Read < storedProduct.Updated;
                if (hasChanged)
                {
                    product = storedProduct;
                }
                else
                {
                    product = await entityScrapper.GetAsync(storedProduct.Url);
                    if (product != null)
                    {
                        product.Id = storedProduct.Id;
                        hasChanged = product.Name != storedProduct.Name
                                     || Math.Abs(product.Price - storedProduct.Price) > 0.001
                                     || product.Store != storedProduct.Store
                                     || product.Currency != storedProduct.Currency
                                     || product.IsAvailable != storedProduct.IsAvailable;

                        if (hasChanged)
                        {
                            product.Updated = dateTime;
                            product = productRepository.TryAddOrUpdate(product, nameof(Product.Added));
                        }
                    }
                }

                if (product != null)
                {
                    product.Read = dateTime;
                    products.Add(
                        new Shared.Product
                        {
                            Id = product.Id,
                            Name = product.Name,
                            Price = product.Price,
                            Url = product.Url,
                            Currency = product.Currency,
                            Store = product.Store,
                            HasChanged = hasChanged,
                            IsAvailable = product.IsAvailable
                        });
                }
            }

            await productRepository.SaveChangesAsync();
            return products;
        }
    }
}