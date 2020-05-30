﻿namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore.Storage;

    using Orc.EntityFrameworkCore;

    using Polly;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.HostedServices;
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
                    var transaction = PolicyHelper.WaitAndRetryForever().Execute(
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
            IDbContextTransaction transaction = null;
            Policy.Handle<Exception>()
                .WaitAndRetryForever(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))).Execute(
                    () => transaction = productRepository.BeginTransaction(IsolationLevel.Serializable));

            productRepository.Delete(product => product.Id == id);
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
                IDbContextTransaction transaction = null;
                Policy.Handle<Exception>()
                    .WaitAndRetryForever(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))).Execute(
                        () => transaction = productRepository.BeginTransaction(IsolationLevel.Serializable));

                storedProduct.Read = DateTime.Now;
                await productRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                products.Add(storedProduct.ToDataTransferObject(hasChanged));
            }

            return products;
        }
    }
}