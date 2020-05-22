namespace YourShipping.Monitor.Client.Pages
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Blorc.Components;
    using Blorc.PatternFly.Components.Table;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using YourShipping.Monitor.Client.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    public class ProductsComponent : BlorcComponentBase
    {
        public bool HasError => !Uri.TryCreate(this.Url, UriKind.Absolute, out _);

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
                            Label = "UnFollow", Action = async o => await this.UnFollow(o as Product)
                        });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        protected async Task AddAsync()
        {
            var product = await this.ApplicationState.FollowProductAsync(this.Url);
            if (product != null)
            {
                this.Url = string.Empty;
                this.StateHasChanged();
            }
        }

        protected bool IsHighlighted(Product product)
        {
            return product != null && product.HasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                this.Products = await this.ApplicationState.GetProductsFromCacheOrFetchAsync();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await this.RefreshAsync(this.ApplicationState.RemoveAlertsFrom(AlertSource.Products));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Products))
            {
                if (this.Products == null)
                {
                    this.ApplicationState.InvalidateProductsCache();
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

        private async Task UnFollow(Product product)
        {
            await this.ApplicationState.UnFollowProductAsync(product);
            this.StateHasChanged();
        }
    }
}