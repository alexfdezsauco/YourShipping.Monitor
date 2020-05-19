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

        Task<Product> FollowProductAsync(string url);

        Task<List<Department>> GetDepartmentsFromCacheOrFetchAsync();

        Task<List<Product>> GetProductsFromCacheOrFetchAsync();

        Task<List<Product>> GetProductsOfDepartmentFromCacheOrFetchAsync(int id);

        bool HasAlertsFrom(AlertSource alertSource);

        void InvalidateDepartmentsCache();

        void InvalidateProductsCache();

        void InvalidatetProductsOfDepartmentCache(int departmentId);

        Task UnFollowProductAsync(Product product);
    }
}