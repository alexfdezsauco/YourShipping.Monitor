namespace YourShipping.Monitor.Server.Extensions
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models;

    public static class IRepositoryExtensions
    {

        public static async Task<(bool, Product)> TryRegisterProductAsync(this IRepository<Product, int> productRepository, string absoluteUrl)
        {
            Log.Information("Try to register product with Url '{Url}'", absoluteUrl);

            var product = productRepository.Find(p => p.Url == absoluteUrl).FirstOrDefault();
            var registered = false;
            if (product == null)
            {
                var dateTime = DateTime.Now;
                product = new Product();
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

                    registered = true;
                }
            }

            Log.Information("Product with Url '{Url}' is registered as {Name}", absoluteUrl, product.Name);

            return (registered, product);
        }
    }
}