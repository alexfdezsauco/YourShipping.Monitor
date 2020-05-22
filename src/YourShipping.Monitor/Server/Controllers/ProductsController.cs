namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
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

            return storedProduct?.ToDataTransferObject();
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
            [FromServices] IEntityScrapper<Models.Product> productScrapper)
        {
            var products = new List<Product>();

            foreach (var storedProduct in productRepository.All())
            {
                var dateTime = DateTime.Now;
                Models.Product product;
                bool hasChanged = false;
                if (storedProduct.Read < storedProduct.Updated)
                {
                    product = storedProduct;
                }
                else
                {
                    product = await productScrapper.GetAsync(storedProduct.Url);
                    if (product == null)
                    {
                        product = storedProduct;
                        if (product.IsAvailable)
                        {
                            hasChanged = true;
                            product.IsAvailable = false;
                            product.Updated = dateTime;
                            product.Sha256 = JsonSerializer.Serialize(storedProduct).ComputeSHA256();
                        }
                    }
                    else if (product.Sha256 != storedProduct.Sha256)
                    {
                        hasChanged = true;
                        product.Id = storedProduct.Id;
                        product.Updated = dateTime;
                        productRepository.TryAddOrUpdate(product, nameof(Models.Product.Added), nameof(Models.Product.Read));
                    }
                }

                product.Read = dateTime;
                products.Add(product.ToDataTransferObject(hasChanged));
            }

            await productRepository.SaveChangesAsync();
            return products;
        }
    }
}