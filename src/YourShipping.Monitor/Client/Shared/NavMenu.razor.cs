namespace YourShipping.Monitor.Client.Shared
{
    using System.Threading.Tasks;

    using Blorc.Components;

    using Microsoft.AspNetCore.Components;

    using YourShipping.Monitor.Client.Services.Interfaces;

    public class NavMenuComponent : BlorcComponentBase
    {
        [Inject]
        protected IApplicationState ApplicationState { get; set; }

        protected override async Task OnInitializedAsync()
        {
            this.ApplicationState.StateChanged += (sender, args) => this.StateHasChanged();
        }
    }
}