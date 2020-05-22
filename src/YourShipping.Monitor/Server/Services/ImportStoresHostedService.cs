namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    using YourShipping.Monitor.Server.Services.Interfaces;

    public sealed class ImportStoresHostedService : IHostedService
    {
        private readonly IServiceProvider serviceProvider;

        private readonly object syncObject = new object();

        private CancellationTokenSource cancellationTokenSource;

        public ImportStoresHostedService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            lock (this.syncObject)
            {
                if (this.cancellationTokenSource == null)
                {
                    this.cancellationTokenSource = new CancellationTokenSource();

                    Task.Run(this.ImportStores, this.cancellationTokenSource.Token);
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (this.syncObject)
            {
                this.cancellationTokenSource?.Cancel();
            }

            return Task.CompletedTask;
        }

        private async Task ImportStores()
        {
            try
            {
                IStoreService storeService;
                try
                {
                    storeService = this.serviceProvider.GetService<IStoreService>();
                }
                catch (InvalidOperationException)
                {
                    var serviceScope = this.serviceProvider.CreateScope();
                    storeService = serviceScope.ServiceProvider.GetService<IStoreService>();
                }

                await storeService.ImportAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error importing stores");
            }
        }
    }
}