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

    public class StoreComponent : BlorcComponentBase
    {
        private readonly Dictionary<object, List<ActionDefinition>> ActionDefinitions =
            new Dictionary<object, List<ActionDefinition>>();

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

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        protected List<Store> Stores
        {
            get => this.GetPropertyValue<List<Store>>(nameof(this.Stores));
            set => this.SetPropertyValue(nameof(this.Stores), value);
        }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            if (!this.ActionDefinitions.ContainsKey(row))
            {
                this.ActionDefinitions[row] = new List<ActionDefinition>();
            }

            var actionDefinitions = this.ActionDefinitions[row];
            if (actionDefinitions.Count == 0)
            {
                if (row is Store store)
                {
                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "Buy",
                                IsDisabled = !store.IsAvailable,
                                Action = async o => await this.BuyOrBrowseAsync(o as Store)
                            });   
                    
                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "Browse",
                                Action = async o => await this.BuyOrBrowseAsync(o as Store)
                            });

                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "Inspect",
                                IsDisabled = !store.IsAvailable,
                                Action = async o => await this.InspectAsync(o as Store)
                            });

                    actionDefinitions.Add(
                        new CallActionDefinition
                            {
                                Label = "UnFollow",
                                IsDisabled = !store.IsStored,
                                Action = async o => await this.UnFollowAsync(o as Store)
                            });

                    var switchActionDefinition = new SwitchActionDefinition
                                                     {
                                                         Label = "Turn-On Scan",
                                                         IsDisabled = !store.IsStored,
                                                         DataContext = store
                                                     };

                    switchActionDefinition.Action = async (dataContext, isChecked) =>
                        {
                            switchActionDefinition.Label =
                                switchActionDefinition.IsChecked ? "Turn-Off Scan" : "Turn-On Scan";
                            if (switchActionDefinition.IsChecked)
                            {
                                await this.ApplicationState.TurnOnScanAsync(dataContext as Store);
                            }
                            else
                            {
                                await this.ApplicationState.TurnOffScanAsync(dataContext as Store);
                            }
                        };

                    // switchActionDefinition.Action = (o, b) =>
                    // {
                    // switchActionDefinition.Label = b ? "Turn-Off Scan" : "Turn-On Scan";
                    // };
                    actionDefinitions.Add(switchActionDefinition);
                }
            }

            foreach (var actionDefinition in actionDefinitions)
            {
                yield return actionDefinition;
            }
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

        protected async Task ImportStoresAsync()
        {
            await this.ApplicationState.ImportStoresAsync();
        }

        protected string GetHighlightStyle(Store store)
        {
            if (store.HasChanged)
            {
                if (store.IsAvailable)
                {
                    return "border-left: 3px solid var(--pf-global--primary-color--100);";
                }

                return "border-left: 3px solid var(--pf-global--danger-color--100);  background-color: var(--pf-global--palette--black-400)";
            }

            if (!store.IsAvailable || (store.CategoriesCount == 0 && store.DepartmentsCount == 0))
            {
                return "border-left: 3px solid var(--pf-global--disabled-color--100); background-color: var(--pf-global--palette--black-400)";
            }

            return string.Empty;
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
            this.ApplicationState.SourceChanged += async (sender, args) =>
                {
                    if (this.ApplicationState.HasAlertsFrom(AlertSource.Stores))
                    {
                        await this.RefreshAsync();
                    }
                };

            this.ApplicationState.RemoveAlertsFrom(AlertSource.Stores);
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading) || e.PropertyName == nameof(this.Url))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Stores) && this.Stores != null)
            {
                this.IsLoading = false;
            }
        }

        protected async Task RefreshAsync(bool reload = false)
        {
            if (reload)
            {
                this.ApplicationState.InvalidateStoresCache();
                this.Stores = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowseAsync(Store store)
        {
            if (store != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", store.Url, "_blank");
            }
        }

        private async Task InspectAsync(Store store)
        {
            this.NavigationManager.NavigateTo($"/inspect-store/{store.Id}");
        }

        private async Task UnFollowAsync(Store store)
        {
            await this.ApplicationState.UnFollowStoreAsync(store);
            this.StateHasChanged();
        }
    }
}