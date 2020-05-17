namespace YourShipping.WishList.Pages
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using Blorc.Components;
    using Blorc.PatternFly.Components.Table;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using YourShipping.Monitor.Shared;

    public class ProductsComponent : BlorcComponentBase
    {
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
                        Label = "Open",
                        IsDisabled = !product.IsAvailable,
                        Action = async o => await this.Open(o as Product)
                    });
                actionDefinitions.Add(
                    new CallActionDefinition { Label = "Delete", Action = async o => await this.Delete(o as Product) });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        protected async Task AddAsync()
        {
            await this.HttpClient.PostAsync("Products", JsonContent.Create(new Uri(this.Url)));
            this.Url = string.Empty;
            await this.RefreshAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                this.Products = (await this.HttpClient.GetFromJsonAsync<Product[]>("Products")).ToList();
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

        protected async Task RefreshAsync()
        {
            this.Products = null;
            this.IsLoading = true;
        }

        private async Task Delete(Product product)
        {
            await this.HttpClient.DeleteAsync($"Products/{product.Id}");
            await this.RefreshAsync();
        }

        private async Task Open(Product product)
        {
            if (product != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", product.Url, "_blank");
            }
        }
    }
}