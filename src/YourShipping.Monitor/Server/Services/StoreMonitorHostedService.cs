namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    public sealed class StoreMonitorHostedService : TimedHostedServiceBase
    {
        public StoreMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Models.Store, int> storeRepository,
            IEntityScrapper<Models.Store> storeScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running Store Monitor.");

            var sourceChanged = false;
            foreach (var storedStore in storeRepository.All())
            {
                var dateTime = DateTime.Now;
                Models.Store store = null;
                try
                {
                    store = await storeScrapper.GetAsync(storedStore.Url);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error scrapping department '{url}'", storedStore.Url);
                }

                if (store == null)
                {
                    if (storedStore.IsAvailable)
                    {
                        storedStore.IsAvailable = false;
                        storedStore.Updated = dateTime;
                        storedStore.Sha256 = JsonSerializer.Serialize(storedStore.IsAvailable).ComputeSHA256();
                        sourceChanged = true;
                    }
                }
                else if (store.Sha256 != storedStore.Sha256)
                {
                    store.Id = storedStore.Id;
                    store.Updated = dateTime;
                    storeRepository.TryAddOrUpdate(store, nameof(Models.Store.Added), nameof(Models.Store.Read));
                    sourceChanged = true;
                }
            }

            await storeRepository.SaveChangesAsync();

            if (sourceChanged)
            {
                Log.Information("{Source} change detected", AlertSource.Stores.ToString());

                await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Stores);
            }
            else
            {
                Log.Information("No {Source} change detected", AlertSource.Stores.ToString());
            }
        }
    }
}