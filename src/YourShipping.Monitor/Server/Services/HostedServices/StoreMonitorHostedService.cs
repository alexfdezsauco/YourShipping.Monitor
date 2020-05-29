namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Data;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Store = YourShipping.Monitor.Server.Models.Store;

    public sealed class StoreMonitorHostedService : TimedHostedServiceBase
    {
        public StoreMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Store, int> storeRepository,
            IEntityScrapper<Store> storeScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Stores);

            var sourceChanged = false;

            foreach (var storedStore in storeRepository.All())
            {
                var dateTime = DateTime.Now;
                var store = await storeScrapper.GetAsync(storedStore.Url, true);
                IDbContextTransaction transaction = null;
                Log.Information("Updating scrapped store '{url}'", storedStore.Url);
                if (store == null)
                {
                    store = storedStore;
                    if (store.IsAvailable)
                    {
                        transaction = storeRepository.BeginTransaction(IsolationLevel.Serializable);
                        store.IsAvailable = false;
                        store.Updated = dateTime;
                        store.Sha256 = JsonSerializer.Serialize(storedStore).ComputeSHA256();
                        sourceChanged = true;

                        Log.Information(
                            "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                            storedStore.Name,
                            storedStore.Province,
                            storedStore.IsAvailable);
                    }
                }
                else if (store.Sha256 != storedStore.Sha256)
                {
                    transaction = storeRepository.BeginTransaction(IsolationLevel.Serializable);
                    store.Id = storedStore.Id;
                    store.Updated = dateTime;
                    storeRepository.TryAddOrUpdate(store, nameof(Store.Added), nameof(Store.Read));
                    sourceChanged = true;

                    Log.Information(
                        "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                        store.Name,
                        store.Province,
                        store.IsAvailable);
                }

                if (transaction != null)
                {
                    await storeRepository.SaveChangesAsync();
                    await transaction.CommitAsync();
                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Stores,
                        JsonSerializer.Serialize(store.ToDataTransferObject(true)));

                    Log.Information("Entity changed at source {Source}.", AlertSource.Stores);
                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Stores,
                        JsonSerializer.Serialize(store.ToDataTransferObject(true)));

                    Log.Information("Entity changed at source {Source}.", AlertSource.Stores);
                }
                else
                {
                    Log.Information("No change detected for store '{url}'", storedStore.Url);
                }
            }

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Stores);
        }
    }
}