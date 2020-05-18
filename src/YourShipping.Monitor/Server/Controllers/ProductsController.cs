namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

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
        public async Task<ActionResult<Product>> Add(
            [FromServices] IRepository<Models.Product, int> productRepository,
            [FromServices] IEntityScrapper<Models.Product> entityScrapper,
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

                    return product.ToDataTransferObject(true);
                }
            }

            return storedProduct.ToDataTransferObject();
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Models.Product, int> productRepository, int id)
        {
            productRepository.Delete(product => product.Id == id);
            await productRepository.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IEnumerable<Product>> Get(
            [FromServices] IRepository<Models.Product, int> productRepository,
            [FromServices] IEntityScrapper<Models.Product> entityScrapper)
        {
            var products = new List<Product>();

            foreach (var storedProduct in productRepository.All())
            {
                var dateTime = DateTime.Now;
                Models.Product product;
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
                        hasChanged = storedProduct.Sha256 != product.Sha256;
                        if (hasChanged)
                        {
                            product.Updated = dateTime;
                            product = productRepository.TryAddOrUpdate(product, nameof(Models.Product.Added));
                        }
                    }
                }

                if (product != null)
                {
                    product.Read = dateTime;
                    products.Add(product.ToDataTransferObject(hasChanged));
                }
            }

            await productRepository.SaveChangesAsync();
            return products;
        }
    }
}