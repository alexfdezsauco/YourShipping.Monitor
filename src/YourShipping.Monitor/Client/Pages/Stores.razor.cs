namespace YourShipping.Monitor.Client.Pages
{
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

    public class StoreComponent : BlorcComponentBase
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
        protected IApplicationState ApplicationState { get; set; }

        [Inject]
        protected HttpClient HttpClient { get; set; }

        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        protected List<Store> Stores
        {
            get => this.GetPropertyValue<List<Store>>(nameof(this.Stores));
            set => this.SetPropertyValue(nameof(this.Stores), value);
        }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            var actionDefinitions = new List<ActionDefinition>();
            if (row is Store store)
            {
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Buy",
                            IsDisabled = !store.IsAvailable,
                            Action = async o => await this.BuyOrBrowse(o as Store)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await this.BuyOrBrowse(o as Store)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Inspect",
                            IsDisabled = !store.IsAvailable,
                            Action = async o => await this.Inspect(o as Store)
                        });
            }

            return actionDefinitions;
        }

        protected async Task AddAsync()
        {
            var store = await this.ApplicationState.AddStoreAsync(this.Url);
            if (store != null)
            {
                this.Url = string.Empty;
                this.StateHasChanged();
            }
        }

        protected bool IsHighlighted(Store store)
        {
            return store != null && store.HasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                this.Stores = await this.ApplicationState.GetStoresFromCacheOrFetchAsync();
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
            else if (e.PropertyName == nameof(this.Stores))
            {
                if (this.Stores == null)
                {
                    this.ApplicationState.InvalidateStoresCache();
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
                this.Stores = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowse(Store store)
        {
            if (store != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", store.Url, "_blank");
            }
        }

        private async Task Inspect(Store store)
        {
            this.NavigationManager.NavigateTo($"/inspect-store/{store.Id}");
        }
    }
}