namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.HostedServices;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class StoreService : IStoreService
    {
        private readonly IEntityScrapper<Store> entityScrapper;

        private readonly IRepository<Store, int> storesRepository;

        public StoreService(IRepository<Store, int> storesRepository, IEntityScrapper<Store> entityScrapper)
        {
            this.storesRepository = storesRepository;
            this.entityScrapper = entityScrapper;
        }

        public async Task<Shared.Store> AddAsync(Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var storedStore = this.storesRepository.Find(store => store.Url == absoluteUrl).FirstOrDefault();
            if (storedStore == null)
            {
                var dateTime = DateTime.Now;
                var store = await this.entityScrapper.GetAsync(absoluteUrl);
                if (store != null)
                {
                    store.Added = dateTime;
                    store.Updated = dateTime;
                    store.Read = dateTime;

                    var transaction = PolicyHelper.WaitAndRetryForever().Execute(
                        () => this.storesRepository.BeginTransaction(IsolationLevel.Serializable));
                    this.storesRepository.Add(store);
                    await this.storesRepository.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return store.ToDataTransferObject(true);
                }
            }

            return storedStore?.ToDataTransferObject();
        }

        public async Task ImportAsync()
        {
            var httpClient = new HttpClient { Timeout = ScrappingConfiguration.HttpClientTimeout };
            OficialStoreInfo[] storesToImport = null;
            try
            {
                storesToImport =
                    await httpClient.GetFromJsonAsync<OficialStoreInfo[]>("https://www.tuenvio.cu/stores.json");
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting stores.json");
            }

            // TODO: Report the status as error.
            if (storesToImport != null)
            {
                var storesUrl = storesToImport.Select(store => new Uri(store.Url));
                foreach (var url in storesUrl)
                {
                    await this.AddAsync(url);
                }
            }
        }
    }
}