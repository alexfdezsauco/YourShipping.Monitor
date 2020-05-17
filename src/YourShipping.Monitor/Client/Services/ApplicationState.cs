namespace YourShipping.Monitor.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http.Connections;
    using Microsoft.AspNetCore.SignalR.Client;

    using YourShipping.Monitor.Client.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    public class ApplicationState : IApplicationState
    {
        private readonly HubConnection connection;


        private readonly List<AlertSource> notifications = new List<AlertSource>();

        public ApplicationState(HubConnectionBuilder hubConnectionBuilder, NavigationManager navigationManager)
        {
            navigationManager.LocationChanged += (sender, args) =>
                {
                    if (args.Location.EndsWith("/departments"))
                    {
                        this.notifications.Remove(AlertSource.Departments);
                        this.OnStateChanged();
                    }
                    else if (args.Location.EndsWith("/products"))
                    {
                        this.notifications.Remove(AlertSource.Products);
                        this.OnStateChanged();
                    }
                };
            this.connection = hubConnectionBuilder.WithUrl(
                $"{navigationManager.BaseUri.TrimEnd('/')}/hubs/messages",
                opt => { opt.Transports = HttpTransportType.WebSockets; }).WithAutomaticReconnect().Build();

            this.connection.On<AlertSource>("DomainChanged", this.OnDomainChanged);
            Task.Run(() => this.connection.StartAsync());
        }

        public event EventHandler StateChanged;

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return this.notifications.Exists(c => c == alertSource);
        }

        public void MarkAsRead(AlertSource alertSource)
        {
            this.notifications.Remove(alertSource);
        }

        protected virtual void OnDomainChanged(AlertSource alertSource)
        {
            if (!this.notifications.Exists(d => d == alertSource))
            {
                this.notifications.Add(alertSource);
                this.OnStateChanged();
            }
        }

        protected virtual void OnStateChanged()
        {
            this.StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}