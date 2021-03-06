﻿namespace YourShipping.Monitor.Client.Pages
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
                            IsDisabled = !product.IsAvailable || !product.IsEnabled,
                            Action = async o => await this.BuyOrBrowseAsync(product)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await this.BuyOrBrowseAsync(product)
                        });

                if (!product.IsEnabled)
                {
                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "Enable",
                                Action = async o => await this.EnableAsync(product)
                            });
                }
                else
                {
                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "Disable",
                                Action = async o => await this.DisableAsync(product)
                            });
                }


                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "UnFollow", Action = async o => await this.UnFollow(o as Product)
                        });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        private async Task EnableAsync(Product product)
        {
            await this.ApplicationState.EnableProductAsync(product.Id);
        }

        private async Task DisableAsync(Product product)
        {
            await this.ApplicationState.DisableProductAsync(product.Id);
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

            if (!product.IsEnabled)
            {
                return "border-left: 3px solid var(--pf-global--disabled-color--100); background-color: var(--pf-global--palette--black-400)";
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
                this.Products = await this.ApplicationState.GetProductsFromCacheOrFetchAsync();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            this.ApplicationState.SourceChanged += async (sender, args) =>
                {
                    if (this.ApplicationState.HasAlertsFrom(AlertSource.Products))
                    {
                        await this.RefreshAsync();
                    }
                };

            this.ApplicationState.RemoveAlertsFrom(AlertSource.Products);
            await this.JsRuntime.InvokeAsync<object>("setTitle", "YourShipping.Monitor");
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
            if (reload)
            {
                this.ApplicationState.InvalidateProductsCache();
                this.Products = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowseAsync(Product product)
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