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

namespace YourShipping.Monitor.Client.Pages
{
    public class StoreComponent : BlorcComponentBase
    {
        private readonly Dictionary<object, List<ActionDefinition>> ActionDefinitions =
            new Dictionary<object, List<ActionDefinition>>();

        public bool HasError => !Uri.TryCreate(Url, UriKind.Absolute, out _);

        public bool IsLoading
        {
            get => GetPropertyValue<bool>(nameof(IsLoading));
            set => SetPropertyValue(nameof(IsLoading), value);
        }

        public string Url
        {
            get => GetPropertyValue<string>(nameof(Url));
            set => SetPropertyValue(nameof(Url), value);
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
            get => GetPropertyValue<List<Store>>(nameof(Stores));
            set => SetPropertyValue(nameof(Stores), value);
        }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            if (!ActionDefinitions.ContainsKey(row))
            {
                ActionDefinitions[row] = new List<ActionDefinition>();
            }

            var actionDefinitions = ActionDefinitions[row];
            if (actionDefinitions.Count == 0)
            {
                if (row is Store store)
                {
                    actionDefinitions.Add(
                        new CallActionDefinition
                        {
                            Label = "Buy",
                            IsDisabled = !store.IsAvailable,
                            Action = async o => await BuyOrBrowseAsync(o as Store)
                        });

                    actionDefinitions.Add(
                        new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await BuyOrBrowseAsync(o as Store)
                        });

                    actionDefinitions.Add(
                        new CallActionDefinition
                        {
                            Label = "Inspect",
                            IsDisabled = !store.IsAvailable,
                            Action = async o => await InspectAsync(o as Store)
                        });

                    actionDefinitions.Add(
                        new CallActionDefinition
                        {
                            Label = "UnFollow",
                            IsDisabled = !store.IsStored,
                            Action = async o => await UnFollowAsync(o as Store)
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
                            await ApplicationState.TurnOnScanAsync(dataContext as Store);
                        }
                        else
                        {
                            await ApplicationState.TurnOffScanAsync(dataContext as Store);
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
            var store = await ApplicationState.AddStoreAsync(Url);
            if (store != null)
            {
                Url = string.Empty;
                StateHasChanged();
            }
        }

        protected string GetHighlightStyle(Store store)
        {
            if (store.HasChanged)
            {
                if (store.IsAvailable)
                {
                    return "border-left: 3px solid var(--pf-global--primary-color--100);";
                }

                if (store.CategoriesCount == 0 && store.DepartmentsCount == 0)
                {
                    return "text-decoration: line-through;";
                }

                return
                    "border-left: 3px solid var(--pf-global--danger-color--100);  color: var(--pf-global--palette--black-400)";
            }

            if (!store.IsAvailable)
            {
                return
                    "border-left: 3px solid var(--pf-global--disabled-color--100); background-color: var(--pf-global--palette--black-400)";
            }

            if (store.CategoriesCount == 0 && store.DepartmentsCount == 0)
            {
                return "text-decoration: line-through;";
            }

            return string.Empty;
        }

        protected async Task ImportStoresAsync()
        {
            await ApplicationState.ImportStoresAsync();
        }


        protected override async Task OnInitializedAsync()
        {
            ApplicationState.SourceChanged += async (sender, args) =>
            {
                if (ApplicationState.HasAlertsFrom(AlertSource.Stores))
                {
                    await RefreshAsync();
                }
            };

            ApplicationState.RemoveAlertsFrom(AlertSource.Stores);
            await JsRuntime.InvokeAsync<object>("setTitle", "YourShipping.Monitor");
            await RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsLoading) || e.PropertyName == nameof(Url))
            {
                StateHasChanged();
            }
            else if (e.PropertyName == nameof(Stores) && Stores != null)
            {
                IsLoading = false;
            }
        }

        protected async Task RefreshAsync(bool reload = false)
        {
            if (reload)
            {
                ApplicationState.InvalidateStoresCache();
                Stores = null;
            }

            IsLoading = true;
        }

        private async Task BuyOrBrowseAsync(Store store)
        {
            if (store != null)
            {
                await JsRuntime.InvokeAsync<object>("open", store.Url, "_blank");
            }
        }

        private async Task InspectAsync(Store store)
        {
            NavigationManager.NavigateTo($"/inspect-store/{store.Id}");
        }

        private async Task UnFollowAsync(Store store)
        {
            await ApplicationState.UnFollowStoreAsync(store);
            StateHasChanged();
        }
    }
}