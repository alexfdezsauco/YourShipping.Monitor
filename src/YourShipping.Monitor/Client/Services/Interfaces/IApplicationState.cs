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

        Task<List<Department>> GetDepartmentsAsync(bool reload = false);

        Task<List<Product>> GetProductsAsync(bool reload = false);

        bool HasAlertsFrom(AlertSource alertSource);
    }
}