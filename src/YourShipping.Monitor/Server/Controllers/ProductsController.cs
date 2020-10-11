namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Models.Extensions;

    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Shared.Product>> Add(
            [FromServices] IRepository<Product, int> productRepository,
            [FromBody] Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var (registered, product) = await productRepository.TryRegisterProductAsync(absoluteUrl);
            return product?.ToDataTransferObject(registered);
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Product, int> productRepository, int id)
        {
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => productRepository.BeginTransaction(IsolationLevel.Serializable));

            productRepository.Delete(product => product.Id == id);
            await productRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        [HttpPut("[action]/{id}")]
        public async Task Disable([FromServices] IRepository<Product, int> productRepository, int id)
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
        public async Task Enable([FromServices] IRepository<Product, int> productRepository, int id)
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
        public async Task<IEnumerable<Shared.Product>> Get([FromServices] IRepository<Product, int> productRepository)
        {
            var products = new List<Shared.Product>();

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