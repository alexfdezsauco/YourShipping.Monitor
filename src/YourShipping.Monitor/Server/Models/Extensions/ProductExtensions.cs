namespace YourShipping.Monitor.Server.Models.Extensions
{
    using YourShipping.Monitor.Shared;

    using Product = YourShipping.Monitor.Server.Models.Product;

    public static class ProductExtensions
    {
        public static Shared.Product ToDataTransferObject(this Product product, bool hasChanged = false)
        {
            return new Shared.Product
                       {
                           Id = product.Id,
                           Name = product.Name,
                           Price = product.Price,
                           Url = product.Url,
                           Currency = product.Currency,
                           Store = product.Store,
                           IsAvailable = product.IsAvailable,
                           HasChanged = hasChanged
                       };
        }
    }
}