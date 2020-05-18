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


        private readonly List<AlertSource> alertSources = new List<AlertSource>();

        public ApplicationState(HubConnectionBuilder hubConnectionBuilder, NavigationManager navigationManager)
        {
            navigationManager.LocationChanged += (sender, args) =>
                {
                    if (args.Location.EndsWith("/departments"))
                    {
                        this.alertSources.Remove(AlertSource.Departments);
                        this.OnStateChanged();
                    }
                    else if (args.Location.EndsWith("/products"))
                    {
                        this.alertSources.Remove(AlertSource.Products);
                        this.OnStateChanged();
                    }
                };
            this.connection = hubConnectionBuilder.WithUrl(
                $"{navigationManager.BaseUri.TrimEnd('/')}/hubs/messages",
                opt => { opt.Transports = HttpTransportType.WebSockets; }).WithAutomaticReconnect().Build();

            this.connection.On<AlertSource>(ClientMethods.SourceChanged, this.OnSourceChanged);
            Task.Run(() => this.connection.StartAsync());
        }

        public event EventHandler SourceChanged;

        public bool HasAlertsFrom(AlertSource alertSource)
        {
            return this.alertSources.Exists(c => c == alertSource);
        }

        protected virtual void OnSourceChanged(AlertSource alertSource)
        {
            if (!this.alertSources.Exists(d => d == alertSource))
            {
                this.alertSources.Add(alertSource);
                this.OnStateChanged();
            }
        }

        protected virtual void OnStateChanged()
        {
            this.SourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}