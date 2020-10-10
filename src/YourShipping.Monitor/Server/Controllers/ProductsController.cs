using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Orc.EntityFrameworkCore;
using YourShipping.Monitor.Server.Helpers;
using YourShipping.Monitor.Server.Models.Extensions;
using YourShipping.Monitor.Server.Services;
using YourShipping.Monitor.Server.Services.Interfaces;
using YourShipping.Monitor.Shared;

namespace YourShipping.Monitor.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Product>> Add(
            [FromServices] IRepository<Models.Product, int> productRepository,
            [FromServices] IEntityScraper<Models.Product> entityScraper,
            [FromBody] Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;

            var storedProduct = productRepository.Find(product => product.Url == absoluteUrl).FirstOrDefault();
            if (storedProduct == null)
            {
                var dateTime = DateTime.Now;
                var product = new Models.Product();
                {
                    product.Name = "Unknown Product";
                    product.Url = UriHelper.EnsureProductUrl(absoluteUrl);
                    product.Added = dateTime;
                    product.Updated = dateTime;
                    product.Read = dateTime;
                    product.IsEnabled = true;
                    var transaction = PolicyHelper.WaitAndRetry().Execute(
                        () => productRepository.BeginTransaction(IsolationLevel.Serializable));

                    productRepository.Add(product);
                    await productRepository.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return product.ToDataTransferObject(true);
                }
            }

            return storedProduct?.ToDataTransferObject();
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Models.Product, int> productRepository, int id)
        {
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => productRepository.BeginTransaction(IsolationLevel.Serializable));

            productRepository.Delete(product => product.Id == id);
            await productRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        [HttpPut("[action]/{id}")]
        public async Task Disable([FromServices] IRepository<Models.Product, int> productRepository, int id)
        {
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => productRepository.BeginTransaction(IsolationLevel.Serializable));

            var product = productRepository.Find(p => p.Id == id).FirstOrDefault();
            if (product != null)
            {
                product.IsEnabled = false;
            }

            await productRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        [HttpPut("[action]/{id}")]
        public async Task Enable([FromServices] IRepository<Models.Product, int> productRepository, int id)
        {
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => productRepository.BeginTransaction(IsolationLevel.Serializable));

            var product = productRepository.Find(p => p.Id == id).FirstOrDefault();
            if (product != null)
            {
                product.IsEnabled = true;
            }

            await productRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        [HttpGet]
        public async Task<IEnumerable<Product>> Get([FromServices] IRepository<Models.Product, int> productRepository)
        {
            var products = new List<Product>();

            foreach (var storedProduct in productRepository.All())
            {
                var hasChanged = storedProduct.Read < storedProduct.Updated;
                var transaction = PolicyHelper.WaitAndRetry().Execute(
                    () => productRepository.BeginTransaction(IsolationLevel.Serializable));

                storedProduct.Read = DateTime.Now;
                await productRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                products.Add(storedProduct.ToDataTransferObject(hasChanged));
            }

            return products;
        }
    }
}