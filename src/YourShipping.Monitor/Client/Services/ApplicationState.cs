namespace YourShipping.Monitor.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http.Connections;
    using Microsoft.AspNetCore.SignalR.Client;

    using YourShipping.Monitor.Client.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    public class ApplicationState : IApplicationState
    {
        private readonly SortedSet<AlertSource> alertSources = new SortedSet<AlertSource>();

        private readonly HubConnection connection;

        private readonly HttpClient httpClient;

        private List<Department> departments;

        private List<Product> products;

        public ApplicationState(
            HubConnectionBuilder hubConnectionBuilder,
            NavigationManager navigationManager,
            HttpClient httpClient)
        {
            this.httpClient = httpClient;
            navigationManager.LocationChanged += (sender, args) =>
                {
                    if (args.Location.EndsWith("/departments-monitor")
                        && this.alertSources.Remove(AlertSource.Departments))
                    {
                        this.OnStateChanged();
                    }
                    else if (args.Location.EndsWith("/products-monitor")
                             && this.alertSources.Remove(AlertSource.Products))
                    {
                        this.OnStateChanged();
                    }
                };
            this.connection = hubConnectionBuilder.WithUrl(
                $"{navigationManager.BaseUri.TrimEnd('/')}/hubs/messages",
                opt => { opt.Transports = HttpTransportType.WebSockets; }).WithAutomaticReconnect().Build();

            this.connection.On<AlertSource>(ClientMethods.SourceChanged, this.OnSourceChanged);
            Task.Run(() => this.connection.StartAsync());
        }

        public event EventHandler SourceChanged;

        public void InvalidateDepartmentsCache()
        {
            this.departments = null;
        }

        public void InvalidateProductsCache()
        {
            this.products = null;
        }

        public async Task<List<Department>> GetDepartmentsAsync(bool reload = false)
        {
            if (this.departments == null || reload)
            {
                this.departments = (await this.httpClient.GetFromJsonAsync<Department[]>("Departments")).ToList();
            }

            return this.departments;
        }

        public async Task<List<Product>> GetProductsAsync(bool reload = false)
        {
            if (this.products == null || reload)
            {
                this.products = (await this.httpClient.GetFromJsonAsync<Product[]>("Products")).ToList();
            }

            return this.products;
        }

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return this.alertSources.Contains(alertSource);
        }

        protected virtual void OnSourceChanged(AlertSource alertSource)
        {
            if (this.alertSources.Add(alertSource))
            {
                this.OnStateChanged();
            }
        }

        protected virtual void OnStateChanged()
        {
            this.SourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}