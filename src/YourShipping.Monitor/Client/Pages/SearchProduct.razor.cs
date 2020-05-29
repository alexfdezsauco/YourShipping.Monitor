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

    public class SearchProductComponent : BlorcComponentBase
    {
        public bool HasError => string.IsNullOrWhiteSpace(this.Keywords);

        public bool IsLoading
        {
            get => this.GetPropertyValue<bool>(nameof(this.IsLoading));
            set => this.SetPropertyValue(nameof(this.IsLoading), value);
        }

        public string Keywords
        {
            get => this.GetPropertyValue<string>(nameof(this.Keywords));
            set => this.SetPropertyValue(nameof(this.Keywords), value);
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

        protected string GetHighlightStyle(Product product)
        {
            if (product.HasChanged)
            {
                if (product.IsAvailable)
                {
                    return "border-left: 3px solid var(--pf-global--primary-color--100);";
                }

                return "border-left: 3px solid var(--pf-global--danger-color--100); text-decoration: line-through;";
            }

            if (!product.IsAvailable)
            {
                return "text-decoration: line-through;";
            }

            return string.Empty;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                // this.Products = await this.ApplicationState.GetProductsFromCacheOrFetchAsync();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Products) && this.Products != null)
            {
                this.IsLoading = false;
            }
        }

        protected async Task RefreshAsync(bool reload = false)
        {
        }

        protected async Task SearchAsync()
        {
            this.Products = await this.ApplicationState.SearchAsync(this.Keywords);
            this.Keywords = string.Empty;
            this.StateHasChanged();
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