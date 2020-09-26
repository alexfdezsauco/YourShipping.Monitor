using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Serilog;
using YourShipping.Monitor.Client.Services.Interfaces;
using YourShipping.Monitor.Shared;

namespace YourShipping.Monitor.Client.Services
{
    public class ApplicationState : IApplicationState
    {
        private readonly SortedSet<AlertSource> alertSources = new SortedSet<AlertSource>();

        private readonly HubConnection connection;

        private readonly Dictionary<int, Department> departments = new Dictionary<int,Department>();

        private readonly Dictionary<int, List<Department>>
            departmentsOfStores = new Dictionary<int, List<Department>>();

        private readonly HttpClient httpClient;

        private readonly IJSRuntime jsRuntime;

        private readonly Dictionary<int, Product> products = new Dictionary<int, Product>();

        private readonly Dictionary<int, List<Product>> productsOfDepartments = new Dictionary<int, List<Product>>();

        private readonly Dictionary<int, Store> stores = new Dictionary<int, Store>();

        public ApplicationState(
            HubConnectionBuilder hubConnectionBuilder,
            NavigationManager navigationManager,
            IJSRuntime jsRuntime,
            HttpClient httpClient)
        {
            this.jsRuntime = jsRuntime;
            this.httpClient = httpClient;
            connection = hubConnectionBuilder.WithUrl(
                $"{navigationManager.BaseUri.TrimEnd('/')}/hubs/messages",
                opt => { opt.Transports = HttpTransportType.WebSockets; }).WithAutomaticReconnect().Build();

            connection.On<AlertSource>(ClientMethods.SourceChanged, OnSourceChanged);
            connection.On<AlertSource, string>(
                ClientMethods.EntityChanged,
                async (source, s) =>
                {
                    await this.jsRuntime.InvokeAsync<object>("setTitle", "YourShipping.Monitor (*)");
                    OnEntityStateChanged(source, s);
                });

            Task.Run(() => connection.StartAsync());
        }

        public event EventHandler SourceChanged;

        public async Task<Department> AddDepartmentAsync(string url)
        {
            var responseMessage = await httpClient.PostAsJsonAsync("Departments", new Uri(url));
            var department = await responseMessage.Content.ReadFromJsonAsync<Department>();
            if (department.HasChanged)
            {
                departments[department.Id] = department;
            }

            return department;
        }

        public async Task<Store> AddStoreAsync(string url)
        {
            var responseMessage = await httpClient.PostAsJsonAsync("Stores", new Uri(url));
            var store = await responseMessage.Content.ReadFromJsonAsync<Store>();
            if (store.HasChanged)
            {
                stores[store.Id] = store;
            }

            return store;
        }

        public async Task DisableProductAsync(int productId)
        {
            await httpClient.PutAsync($"Products/Disable/{productId}", null);
        }

        public async Task EnableProductAsync(int productId)
        {
            await httpClient.PutAsync($"Products/Enable/{productId}", null);
        }

        public async Task<Department> FollowDepartmentAsync(string productUrl)
        {
            var responseMessage = await httpClient.PostAsJsonAsync(
                "Departments",
                new Uri(productUrl));
            var department = await responseMessage.Content.ReadFromJsonAsync<Department>();
            if (department.HasChanged)
            {
                departments[department.Id]= department;
            }

            return department;
        }

        public async Task<Product> FollowProductAsync(string url)
        {
            var responseMessage = await httpClient.PostAsJsonAsync("Products", new Uri(url));
            var product = await responseMessage.Content.ReadFromJsonAsync<Product>();
            if (product.HasChanged)
            {
                products[product.Id] = product;
            }

            return product;
        }

        public async Task<List<Department>> GetDepartmentsFromCacheOrFetchAsync()
        {
            if (departments.Count == 0)
            {
                var receivedDepartments = await httpClient.GetFromJsonAsync<Department[]>("Departments");
                foreach (var department in receivedDepartments)
                {
                    departments[department.Id] = department;
                }
            }

            return departments.Values.ToList();
        }

        public async Task<List<Department>> GetDepartmentsOfStoreFromCacheOrFetchAsync(int id)
        {
            if (!departmentsOfStores.ContainsKey(id))
            {
                departmentsOfStores[id] =
                    (await httpClient.GetFromJsonAsync<Department[]>($"Stores/GetDepartments/{id}")).ToList();
            }
            else if (departmentsOfStores[id].Count == 0)
            {
                departmentsOfStores[id].AddRange(
                    await httpClient.GetFromJsonAsync<Department[]>($"Stores/GetDepartments/{id}"));
            }

            return departmentsOfStores[id];
        }

        public async Task<List<Product>> GetProductsFromCacheOrFetchAsync()
        {
            if (products.Count == 0)
            {
                var receivedProducts = await httpClient.GetFromJsonAsync<Product[]>("Products");
                foreach (var product in receivedProducts)
                {
                    products[product.Id] = product;
                }
            }

            return products.Values.ToList();
        }

        public async Task<List<Product>> GetProductsOfDepartmentFromCacheOrFetchAsync(int id)
        {
            if (!productsOfDepartments.ContainsKey(id))
            {
                productsOfDepartments[id] =
                    (await httpClient.GetFromJsonAsync<Product[]>($"Departments/GetProducts/{id}")).ToList();
            }
            else if (productsOfDepartments[id].Count == 0)
            {
                productsOfDepartments[id].AddRange(
                    await httpClient.GetFromJsonAsync<Product[]>($"Departments/GetProducts/{id}"));
            }

            return productsOfDepartments[id];
        }

        public async Task<List<Store>> GetStoresFromCacheOrFetchAsync()
        {
            if (stores.Count == 0)
            {
                var retrievedStores = await httpClient.GetFromJsonAsync<Store[]>("Stores");
                foreach (var store in retrievedStores)
                {
                    stores[store.Id] = store;
                }
            }

            return stores.Values.ToList();
        }

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return alertSources.Contains(alertSource);
        }

        public async Task ImportStoresAsync()
        {
            await httpClient.PostAsync("HostedService/StartImportStores", null);
        }

        public void InvalidateDepartmentsCache()
        {
            departments?.Clear();
        }

        public void InvalidateDepartmentsOfStoreCache(int storeId)
        {
            departmentsOfStores.TryGetValue(storeId, out var departmentsOfStore);
            departmentsOfStore?.Clear();
        }

        public void InvalidateProductsCache()
        {
            products?.Clear();
        }

        public void InvalidateProductsOfDepartmentCache(int departmentId)
        {
            productsOfDepartments.TryGetValue(departmentId, out var productsOfDepartment);
            productsOfDepartment?.Clear();
        }

        public void InvalidateStoresCache()
        {
            stores?.Clear();
        }

        public bool RemoveAlertsFrom(AlertSource alertSource)
        {
            if (alertSources.Remove(alertSource))
            {
                OnStateChanged();
                return true;
            }

            return false;
        }

        public async Task<List<Product>> SearchAsync(string keywords)
        {
            try
            {
                var searchResults =
                    await httpClient.GetFromJsonAsync<Product[]>($"Stores/Search?keywords={keywords}");
                return searchResults?.ToList();
            }
            catch (Exception e)
            {
                Log.Error(e.Message);

                return null;
            }
        }

        public async Task TurnOffScanAsync(Store store)
        {
            await httpClient.PostAsync($"Stores/TurnOffScan/{store.Id}", null);
        }

        public async Task TurnOnScanAsync(Store store)
        {
            await httpClient.PostAsync($"Stores/TurnOnScan/{store.Id}", null);
        }

        public async Task UnFollowDepartmentAsync(Department department)
        {
            await httpClient.DeleteAsync($"Departments/{department.Id}");
        }

        public async Task UnFollowProductAsync(Product product)
        {
            await httpClient.DeleteAsync($"Products/{product.Id}");

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
            await httpClient.DeleteAsync($"Stores/{store.Id}");
        }

        protected virtual void OnEntityStateChanged(AlertSource alertSource, string serializedEntity)
        {
            switch (alertSource)
            {
                case AlertSource.Products:
                {
                    var receivedProduct = JsonSerializer.Deserialize<Product>(serializedEntity);
                    if (products.TryGetValue(receivedProduct.Id, out var storedProduct))
                    {
                        storedProduct.Url = receivedProduct.Url;
                        storedProduct.IsAvailable = receivedProduct.IsAvailable;
                        storedProduct.Department = receivedProduct.Department;
                        storedProduct.Currency = receivedProduct.Currency;
                        storedProduct.Store = receivedProduct.Store;
                        storedProduct.IsStored = receivedProduct.IsStored;
                        storedProduct.Name = receivedProduct.Name;
                        storedProduct.HasChanged = receivedProduct.HasChanged;
                    }

                    break;
                }

                case AlertSource.Departments:
                {
                    var receivedDepartment = JsonSerializer.Deserialize<Department>(serializedEntity);
                    if (departments.TryGetValue(receivedDepartment.Id, out var storedDepartment))
                    {
                        storedDepartment.Url = receivedDepartment.Url;
                        storedDepartment.Name = receivedDepartment.Name;
                        storedDepartment.Category = receivedDepartment.Category;
                        storedDepartment.ProductsCount = receivedDepartment.ProductsCount;
                        storedDepartment.Store = receivedDepartment.Store;
                        storedDepartment.IsAvailable = receivedDepartment.IsAvailable;
                        storedDepartment.IsStored = receivedDepartment.IsStored;
                        storedDepartment.HasChanged = receivedDepartment.HasChanged;
                    }

                    break;
                }

                case AlertSource.Stores:
                {
                    var receivedStore = JsonSerializer.Deserialize<Store>(serializedEntity);
                    if (stores.TryGetValue(receivedStore.Id, out var storedStore))
                    {
                        storedStore.Url = receivedStore.Url;
                        storedStore.Name = receivedStore.Name;
                        storedStore.Province = receivedStore.Province;
                        storedStore.DepartmentsCount = receivedStore.DepartmentsCount;
                        storedStore.CategoriesCount = receivedStore.CategoriesCount;
                        storedStore.IsAvailable = receivedStore.IsAvailable;
                        storedStore.IsStored = receivedStore.IsStored;
                        storedStore.HasChanged = receivedStore.HasChanged;
                    }

                    break;
                }
            }

            OnSourceChanged(alertSource);
        }

        protected virtual void OnSourceChanged(AlertSource alertSource)
        {
            if (alertSources.Add(alertSource))
            {
                OnStateChanged();
            }
        }

        protected virtual void OnStateChanged()
        {
            SourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}