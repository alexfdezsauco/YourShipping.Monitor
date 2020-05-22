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

        private readonly List<Department> departments = new List<Department>();

        private readonly Dictionary<int, List<Department>> departmentsOfStores = new Dictionary<int, List<Department>>();

        private readonly HttpClient httpClient;

        private readonly List<Product> products = new List<Product>();

        private readonly Dictionary<int, List<Product>> productsOfDepartments = new Dictionary<int, List<Product>>();

        private readonly List<Store> stores = new List<Store>();

        public ApplicationState(
            HubConnectionBuilder hubConnectionBuilder,
            NavigationManager navigationManager,
            HttpClient httpClient)
        {
            this.httpClient = httpClient;
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

        public async Task<Store> AddStoreAsync(string url)
        {
            var responseMessage = await this.httpClient.PostAsync("Stores", JsonContent.Create(new Uri(url)));
            var store = await responseMessage.Content.ReadFromJsonAsync<Store>();
            if (store.HasChanged)
            {
                this.stores?.Add(store);
            }

            return store;
        }

        public async Task<Department> FollowDepartmentAsync(string productUrl)
        {
            var responseMessage = await this.httpClient.PostAsync(
                                      "Departments",
                                      JsonContent.Create(new Uri(productUrl)));
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
            if (this.departments.Count == 0)
            {
                this.departments.AddRange(await this.httpClient.GetFromJsonAsync<Department[]>("Departments"));
            }

            return this.departments;
        }

        public async Task<List<Department>> GetDepartmentsOfStoreFromCacheOrFetchAsync(int id)
        {
            if (!this.departmentsOfStores.ContainsKey(id))
            {
                this.departmentsOfStores[id] =
                    (await this.httpClient.GetFromJsonAsync<Department[]>($"Stores/GetDepartments/{id}")).ToList();
            }
            else if (this.departmentsOfStores[id].Count == 0)
            {
                this.departmentsOfStores[id].AddRange(
                    await this.httpClient.GetFromJsonAsync<Department[]>($"Stores/GetDepartments/{id}"));
            }

            return this.departmentsOfStores[id];
        }

        public async Task<List<Product>> GetProductsFromCacheOrFetchAsync()
        {
            if (this.products.Count == 0)
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

        public async Task<List<Store>> GetStoresFromCacheOrFetchAsync()
        {
            if (this.stores.Count == 0)
            {
                this.stores.AddRange(await this.httpClient.GetFromJsonAsync<Store[]>("Stores"));
            }

            return this.stores;
        }

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return this.alertSources.Contains(alertSource);
        }

        public async Task ImportStoresAsync()
        {
            await this.httpClient.PostAsync("HostedService/StartImportStores", null);
        }

        public void InvalidateDepartmentsCache()
        {
            this.departments?.Clear();
        }

        public void InvalidateDepartmentsOfStoreCache(int storeId)
        {
            this.departmentsOfStores.TryGetValue(storeId, out var departmentsOfStore);
            departmentsOfStore?.Clear();
        }

        public void InvalidateProductsCache()
        {
            this.products?.Clear();
        }

        public void InvalidateProductsOfDepartmentCache(int departmentId)
        {
            this.productsOfDepartments.TryGetValue(departmentId, out var productsOfDepartment);
            productsOfDepartment?.Clear();
        }

        public void InvalidateStoresCache()
        {
            this.stores?.Clear();
        }

        public bool RemoveAlertsFrom(AlertSource alertSource)
        {
            if (this.alertSources.Remove(alertSource))
            {
                this.OnStateChanged();
                return true;
            }

            return false;
        }

        public async Task TurnOffScanAsync(Store store)
        {
            await this.httpClient.PostAsync($"Stores/TurnOffScan/{store.Id}", null);
        }

        public async Task TurnOnScanAsync(Store store)
        {
            await this.httpClient.PostAsync($"Stores/TurnOnScan/{store.Id}", null);
        }

        public async Task UnFollowDepartmentAsync(Department department)
        {
            await this.httpClient.DeleteAsync($"Departments/{department.Id}");
        }

        public async Task UnFollowProductAsync(Product product)
        {
            await this.httpClient.DeleteAsync($"Products/{product.Id}");

            // if (this.products.Remove(product))
            // {
            // if (this.productsOfDepartments != null)
            // {
            // foreach (var productsOfDepartment in this.productsOfDepartments)
            // {
            // var cachedProduct = productsOfDepartment.Value?.FirstOrDefault(p => p.Url == product.Url);
            // if (cachedProduct != null)
            // {
            // cachedProduct.IsStored = false;
            // cachedProduct.HasChanged = false;
            // }
            // }
            // }
            // }
            // else
            // {
            // this.products.RemoveAll(p => p.Url == product.Url);
            // product.IsStored = false;
            // product.HasChanged = false;
            // }
        }

        public async Task UnFollowStoreAsync(Store store)
        {
            await this.httpClient.DeleteAsync($"Stores/{store.Id}");
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