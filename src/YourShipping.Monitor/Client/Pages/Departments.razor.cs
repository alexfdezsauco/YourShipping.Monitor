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

    public class DepartmentsComponent : BlorcComponentBase
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
        protected NavigationManager NavigationManager { get; set; }

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
                            Label = "Inspect",
                            IsDisabled = department.ProductsCount == 0,
                            Action = async o => await this.Inspect(o as Department)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Delete", Action = async o => await this.Delete(o as Department)
                        });
                actionDefinitions.Add(new SeparatorActionDefinition());
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Add all products",
                            IsDisabled = department.ProductsCount == 0,
                            Action = async o => await this.AddAll(o as Department)
                        });

                return actionDefinitions;
            }

            return actionDefinitions;
        }

        protected async Task AddAsync()
        {
            var department = await this.ApplicationState.AddDepartmentAsync(this.Url);
            if (department != null)
            {
                this.Url = string.Empty;
                this.StateHasChanged();
            }
        }

        protected bool IsHighlighted(Department department)
        {
            return department != null && department.HasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (this.IsLoading)
            {
                this.Departments = await this.ApplicationState.GetDepartmentsFromCacheOrFetchAsync();
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
            else if (e.PropertyName == nameof(this.Departments))
            {
                if (this.Departments == null)
                {
                    this.ApplicationState.InvalidateDepartmentsCache();
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

        private async Task AddAll(Department department)
        {
            throw new NotImplementedException();
        }

        private async Task BuyOrBrowse(Department department)
        {
            if (department != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", department.Url, "_blank");
            }
        }

        private async Task Delete(Department department)
        {
            await this.HttpClient.DeleteAsync($"Departments/{department.Id}");
            this.Departments.Remove(department);
            this.StateHasChanged();
        }

        private async Task Inspect(Department department)
        {
            this.NavigationManager.NavigateTo($"/inspect-department/{department.Id}");
        }
    }
}