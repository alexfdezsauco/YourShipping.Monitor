namespace YourShipping.Monitor.Client.Services.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Shared;

    public interface IApplicationState
    {
        event EventHandler SourceChanged;

        Task<Department> AddDepartmentAsync(string url);

        Task<Store> AddStoreAsync(string url);

        Task DisableProductAsync(int productId);

        Task EnableProductAsync(int productId);

        Task<Department> FollowDepartmentAsync(string productUrl);

        Task<Product> FollowProductAsync(string url);

        Task<List<Department>> GetDepartmentsFromCacheOrFetchAsync();

        Task<List<Department>> GetDepartmentsOfStoreFromCacheOrFetchAsync(int parse);

        Task<List<Product>> GetProductsFromCacheOrFetchAsync();

        Task<List<Product>> GetProductsOfDepartmentFromCacheOrFetchAsync(int id);

        Task<List<Store>> GetStoresFromCacheOrFetchAsync();

        bool HasAlertsFrom(AlertSource alertSource);

        Task ImportStoresAsync();

        void InvalidateDepartmentsCache();

        void InvalidateDepartmentsOfStoreCache(int storeId);

        void InvalidateProductsCache();

        void InvalidateProductsOfDepartmentCache(int departmentId);

        void InvalidateStoresCache();

        bool RemoveAlertsFrom(AlertSource alertSource);

        Task<List<Product>> SearchAsync(string keywords);

        Task TurnOffScanAsync(Store store);

        Task TurnOnScanAsync(Store store);

        Task UnFollowDepartmentAsync(Department department);

        Task UnFollowProductAsync(Product product);

        Task UnFollowStoreAsync(Store store);
    }
}