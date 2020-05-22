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

    public class IspectStoreComponent : BlorcComponentBase
    {
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

        public Store Store
        {
            get => this.GetPropertyValue<Store>(nameof(this.Store));
            set => this.SetPropertyValue(nameof(this.Store), value);
        }

        public string Url
        {
            get => this.GetPropertyValue<string>(nameof(this.Url));
            set => this.SetPropertyValue(nameof(this.Url), value);
        }

        [Inject]
        protected IApplicationState ApplicationState { get; set; }

        protected List<Department> Departments
        {
            get => this.GetPropertyValue<List<Department>>(nameof(this.Departments));
            set => this.SetPropertyValue(nameof(this.Departments), value);
        }

        [Inject]
        protected HttpClient HttpClient { get; set; }

        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        public IEnumerable<ActionDefinition> GetActions(object row)
        {
            var actionDefinitions = new List<ActionDefinition>();
            if (row is Department department)
            {
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Buy",
                            IsDisabled = department.ProductsCount == 0,
                            Action = async o => await this.BuyOrBrowse(o as Department)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await this.BuyOrBrowse(o as Department)
                        });

                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Follow",
                            IsDisabled = department.IsStored,
                            Action = async o => await this.Follow(o as Department)
                        });

                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "UnFollow",
                            IsDisabled = !department.IsStored,
                            Action = async o => await this.UnFollow(o as Department)
                        });
            }

            return actionDefinitions;
        }

        protected bool IsHighlighted(Department department)
        {
            return department != null && department.HasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading && !string.IsNullOrWhiteSpace(this.Id))
            {
                this.Departments =
                    await this.ApplicationState.GetDepartmentsOfStoreFromCacheOrFetchAsync(int.Parse(this.Id));
            }
        }

        protected override async Task OnInitializedAsync()
        {
            this.Store = await this.HttpClient.GetFromJsonAsync<Store>($"Stores/{this.Id}");
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Departments))
            {
                if (this.Departments == null && !string.IsNullOrWhiteSpace(this.Id))
                {
                    this.ApplicationState.InvalidateDepartmentsOfStoreCache(int.Parse(this.Id));
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
                this.Departments = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowse(Department department)
        {
            if (department != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", department.Url, "_blank");
            }
        }

        private async Task Follow(Department department)
        {
            var departmentUrl = department.Url;
            var storedProduct = await this.ApplicationState.FollowDepartmentAsync(departmentUrl);

            // TODO: Improve this.
            if (storedProduct != null)
            {
                var cachedProduct = this.Departments.Find(p => p.Url == departmentUrl);
                cachedProduct.Id = storedProduct.Id;
                cachedProduct.IsStored = storedProduct.IsStored;
                cachedProduct.HasChanged = storedProduct.HasChanged;
            }

            await this.RefreshAsync();
        }

        private async Task UnFollow(Department department)
        {
            await this.ApplicationState.UnFollowDepartmentAsync(department);
            this.StateHasChanged();
        }
    }
}