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
                            IsDisabled = !department.IsAvailable || department.ProductsCount == 0,
                            Action = async o => await this.BuyOrBrowseAsync(o as Department)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Browse", Action = async o => await this.BuyOrBrowseAsync(o as Department)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "Inspect",
                            IsDisabled = !department.IsAvailable || department.ProductsCount == 0,
                            Action = async o => await this.InspectAsync(o as Department)
                        });
                actionDefinitions.Add(
                    new CallActionDefinition
                        {
                            Label = "UnFollow", Action = async o => await this.UnFollowAsync(o as Department)
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

        protected string GetHighlightStyle(Department department)
        {
            if (department != null)
            {
                if (department.HasChanged)
                {
                    if (department.IsAvailable && department.ProductsCount > 0)
                    {
                        return "border-left: 3px solid var(--pf-global--primary-color--100);";
                    }

                    if (department.ProductsCount == 0)
                    {
                        return "text-decoration: line-through;";
                    }

                    return "border-left: 3px solid var(--pf-global--danger-color--100); color: var(--pf-global--palette--black-400)";
                }

                if (!department.IsAvailable)
                {
                    return "border-left: 3px solid var(--pf-global--disabled-color--100);  color: var(--pf-global--palette--black-400)";
                }

                if (department.ProductsCount == 0)
                {
                    return "text-decoration: line-through;";
                }
            }

            return string.Empty;
        }

        protected override async Task OnInitializedAsync()
        {
            this.ApplicationState.SourceChanged += async (sender, args) =>
                {
                    if (this.ApplicationState.HasAlertsFrom(AlertSource.Departments))
                    {
                        await this.RefreshAsync();
                    }
                };

            this.ApplicationState.RemoveAlertsFrom(AlertSource.Departments);
            await this.JsRuntime.InvokeAsync<object>("setTitle", "YourShipping.Monitor");
            await this.RefreshAsync();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.IsLoading))
            {
                this.StateHasChanged();
            }
            else if (e.PropertyName == nameof(this.Departments) && this.Departments != null)
            {
                this.IsLoading = false;
            }
        }

        protected async Task RefreshAsync(bool reload = false)
        {
            if (reload)
            {
                this.ApplicationState.InvalidateDepartmentsCache();
                this.Departments = null;
            }

            this.IsLoading = true;
        }

        private async Task BuyOrBrowseAsync(Department department)
        {
            if (department != null)
            {
                await this.JsRuntime.InvokeAsync<object>("open", department.Url, "_blank");
            }
        }

        private async Task InspectAsync(Department department)
        {
            this.NavigationManager.NavigateTo($"/inspect-department/{department.Id}");
        }

        private async Task UnFollowAsync(Department department)
        {
            await this.HttpClient.DeleteAsync($"Departments/{department.Id}");
            this.Departments.Remove(department);
            this.StateHasChanged();
        }
    }
}