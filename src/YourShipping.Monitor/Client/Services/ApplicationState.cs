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

        private readonly Dictionary<int, List<Product>> productsOfDepartments = new Dictionary<int, List<Product>>();

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

        public async Task<Department> AddDepartmentAsync(string url)
        {
            var responseMessage = await this.httpClient.PostAsync("Departments", JsonContent.Create(new Uri(url)));
            var department = await responseMessage.Content.ReadFromJsonAsync<Department>();
            if (department.HasChanged)
            {
                this.departments?.Add(department);
            }

            return department;
        }

        public async Task<Product> FollowProductAsync(string url)
        {
            var responseMessage = await this.httpClient.PostAsync("Products", JsonContent.Create(new Uri(url)));
            var product = await responseMessage.Content.ReadFromJsonAsync<Product>();
            if (product.HasChanged)
            {
                this.products?.Add(product);
            }

            return product;
        }

        public async Task<List<Department>> GetDepartmentsFromCacheOrFetchAsync()
        {
            if (this.departments == null)
            {
                this.departments = (await this.httpClient.GetFromJsonAsync<Department[]>("Departments")).ToList();
            }
            else if (this.departments.Count == 0)
            {
                this.departments.AddRange(await this.httpClient.GetFromJsonAsync<Department[]>("Departments"));
            }

            return this.departments;
        }

        public async Task<List<Product>> GetProductsFromCacheOrFetchAsync()
        {
            if (this.products == null)
            {
                this.products = (await this.httpClient.GetFromJsonAsync<Product[]>("Products")).ToList();
            }
            else if (this.products.Count == 0)
            {
                this.products.AddRange(await this.httpClient.GetFromJsonAsync<Product[]>("Products"));
            }

            return this.products;
        }

        public async Task<List<Product>> GetProductsOfDepartmentFromCacheOrFetchAsync(int id)
        {
            if (!this.productsOfDepartments.ContainsKey(id))
            {
                this.productsOfDepartments[id] =
                    (await this.httpClient.GetFromJsonAsync<Product[]>($"Departments/GetProducts/{id}")).ToList();
            }
            else if (this.productsOfDepartments[id].Count == 0)
            {
                this.productsOfDepartments[id].AddRange(
                    await this.httpClient.GetFromJsonAsync<Product[]>($"Departments/GetProducts/{id}"));
            }

            return this.productsOfDepartments[id];
        }

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return this.alertSources.Contains(alertSource);
        }

        public void InvalidateDepartmentsCache()
        {
            this.departments?.Clear();
        }

        public void InvalidateProductsCache()
        {
            this.products?.Clear();
        }

        public void InvalidatetProductsOfDepartmentCache(int departmentId)
        {
            this.productsOfDepartments.TryGetValue(departmentId, out var productsOfDepartment);
            productsOfDepartment?.Clear();
        }

        public async Task UnFollowProductAsync(Product product)
        {
            await this.httpClient.DeleteAsync($"Products/{product.Id}");
            if (this.products.Remove(product))
            {
                if (this.productsOfDepartments != null)
                {
                    foreach (var productsOfDepartment in this.productsOfDepartments)
                    {
                        var cachedProduct = productsOfDepartment.Value?.FirstOrDefault(p => p.Url == product.Url);
                        if (cachedProduct != null)
                        {
                            cachedProduct.IsStored = false;
                            cachedProduct.HasChanged = false;
                        }
                    }
                }
            }
            else
            {
                this.products.RemoveAll(p => p.Url == product.Url);
                product.IsStored = false;
                product.HasChanged = false;
            }
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