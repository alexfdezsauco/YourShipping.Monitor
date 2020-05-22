namespace YourShipping.Monitor.Client.Pages
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using Blorc.Components;
    using Blorc.PatternFly.Components.Table;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using YourShipping.Monitor.Client.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    public class InspectDepartmentComponent : BlorcComponentBase
    {
        public Department Department
        {
            get => this.GetPropertyValue<Department>(nameof(this.Department));
            set => this.SetPropertyValue(nameof(this.Department), value);
        }

        [Parameter]
        public string Id
        {
            get => this.GetPropertyValue<string>(nameof(this.Id));
            set => this.SetPropertyValue(nameof(this.Id), value);
        }

        public bool IsLoading
        {
            get => this.GetPropertyValue<bool>(nameof(this.IsLoading));
            set => this.SetPropertyValue(nameof(this.IsLoading), value);
        }

        public string Url
        {
            get => this.GetPropertyValue<string>(nameof(this.Url));
            set => this.SetPropertyValue(nameof(this.Url), value);
        }

        [Inject]
        protected IApplicationState ApplicationState { get; set; }

        [Inject]
        protected HttpClient HttpClient { get; set; }

        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        protected List<Product> Products
        {
            get => this.GetPropertyValue<List<Product>>(nameof(this.Products));
            set => this.SetPropertyValue(nameof(this.Products), value);
        }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            var actionDefinitions = new List<ActionDefinition>();
            if (row is Product product)
            {
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Buy",
                            IsDisabled = !product.IsAvailable,
                            Action = async o => await this.BuyOrBrowse(o as Product)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await this.BuyOrBrowse(o as Product)
                        });

                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Follow",
                            IsDisabled = product.IsStored,
                            Action = async o => await this.Follow(o as Product)
                        });

                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "UnFollow",
                            IsDisabled = !product.IsStored,
                            Action = async o => await this.UnFollow(o as Product)
                        });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        protected bool IsHighlighted(Product product)
        {
            return product != null && product.HasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading && !string.IsNullOrWhiteSpace(this.Id))
            {
                this.Products =
                    await this.ApplicationState.GetProductsOfDepartmentFromCacheOrFetchAsync(int.Parse(this.Id));
            }
        }

        protected override async Task OnInitializedAsync()
        {
            this.Department = await this.HttpClient.GetFromJsonAsync<Department>($"Departments/{this.Id}");
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Products))
            {
                if (this.Products == null && !string.IsNullOrWhiteSpace(this.Id))
                {
                    this.ApplicationState.InvalidateProductsOfDepartmentCache(int.Parse(this.Id));
                }
                else
                {
                    this.IsLoading = false;
                }
            }
        }

        protected async Task RefreshAsync(bool reload = false)
        {
            if (reload)
            {
                this.Products = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowse(Product product)
        {
            if (product != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", product.Url, "_blank");
            }
        }

        private async Task Follow(Product product)
        {
            var productUrl = product.Url;
            var storedProduct = await this.ApplicationState.FollowProductAsync(productUrl);
            if (storedProduct != null)
            {
                var cachedProduct = this.Products.Find(p => p.Url == productUrl);
                cachedProduct.Id = storedProduct.Id;
                cachedProduct.IsStored = storedProduct.IsStored;
                cachedProduct.HasChanged = storedProduct.HasChanged;
            }

            await this.RefreshAsync();
        }

        private async Task UnFollow(Product product)
        {
            await this.ApplicationState.UnFollowProductAsync(product);
            this.StateHasChanged();
        }
    }
}