namespace YourShipping.Monitor.Client.Services.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Shared;

    public interface IApplicationState
    {
        event EventHandler SourceChanged;

        void InvalidateDepartmentsCache();

        void InvalidateProductsCache();

        Task<List<Department>> GetDepartmentsFromCacheOrFetchAsync();

        Task<List<Product>> GetProductsFromCacheOrFetchAsync();

        bool HasAlertsFrom(AlertSource alertSource);
    }
}