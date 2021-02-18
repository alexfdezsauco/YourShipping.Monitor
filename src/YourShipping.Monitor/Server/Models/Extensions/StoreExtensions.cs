namespace YourShipping.Monitor.Server.Models.Extensions
{
    using System.IO;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Shared;

    using Store = YourShipping.Monitor.Server.Models.Store;

    public static class StoreExtensions
    {
        public static Shared.Store ToDataTransferObject(this Store store, bool hasChanged = false, bool stored = true)
        {
            var storeSlug = UriHelper.GetStoreSlug(store.Url);
            var path = $"captchas/{storeSlug}.jpg";

            return new Shared.Store
                       {
                           Id = store.Id,
                           Url = store.Url,
                           Name = store.Name,
                           HasChanged = hasChanged,
                           CategoriesCount = store.CategoriesCount,
                           Province = store.Province,
                           DepartmentsCount = store.DepartmentsCount,
                           IsEnabled = store.IsEnabled,
                           IsAvailable = store.IsAvailable,
                           IsStored = stored,
                           HasProductsInCart = store.HasProductsInCart,
                           Captcha = File.Exists(path)
                       };
        }
    }
}