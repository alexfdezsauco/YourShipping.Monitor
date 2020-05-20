namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Models.Extensions;
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

                    this.storesRepository.Add(store);
                    await this.storesRepository.SaveChangesAsync();

                    return store.ToDataTransferObject(true);
                }
            }

            return storedStore?.ToDataTransferObject();
        }
    }
}