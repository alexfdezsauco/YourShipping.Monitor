﻿using System;
using System.Data;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Orc.EntityFrameworkCore;
using Serilog;
using YourShipping.Monitor.Server.Helpers;
using YourShipping.Monitor.Server.Models;
using YourShipping.Monitor.Server.Models.Extensions;
using YourShipping.Monitor.Server.Services.Interfaces;

namespace YourShipping.Monitor.Server.Services
{
    public class StoreService : IStoreService
    {
        private readonly IEntityScraper<Store> _entityScraper;
        private readonly ICookiesAwareHttpClientFactory cookiesAwareHttpClientFactory;

        private readonly IRepository<Store, int> storesRepository;

        public StoreService(
            IRepository<Store, int> storesRepository,
            IEntityScraper<Store> entityScraper,
            ICookiesAwareHttpClientFactory cookiesAwareHttpClientFactory)
        {
            this.storesRepository = storesRepository;
            _entityScraper = entityScraper;
            this.cookiesAwareHttpClientFactory = cookiesAwareHttpClientFactory;
        }

        public async Task<Shared.Store> AddAsync(Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var storedStore = storesRepository.Find(store => store.Url == absoluteUrl).FirstOrDefault();
            if (storedStore == null)
            {
                var dateTime = DateTime.Now;
                var store = new Store
                {
                    Name = "Unknown Store",
                    IsEnabled = true,
                    Url = UriHelper.EnsureStoreUrl(absoluteUrl),
                    Added = dateTime,
                    Updated = dateTime,
                    Read = dateTime
                };

                var transaction = PolicyHelper.WaitAndRetry().Execute(
                    () => storesRepository.BeginTransaction(IsolationLevel.Serializable));

                storesRepository.Add(store);
                await storesRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return store.ToDataTransferObject(true);
            }

            return storedStore?.ToDataTransferObject();
        }

        public async Task ImportAsync()
        {
            var httpClient =
                await this.cookiesAwareHttpClientFactory.CreateHttpClientAsync(ScraperConfigurations.StoresJsonUrl);
            OfficialStoreInfo[] storesToImport = null;
            try
            {
                storesToImport =
                    await httpClient.GetFromJsonAsync<OfficialStoreInfo[]>(ScraperConfigurations.StoresJsonUrl);
                await this.cookiesAwareHttpClientFactory.SyncCookiesAsync(ScraperConfigurations.StoresJsonUrl, httpClient);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting stores.json");

                this.cookiesAwareHttpClientFactory.InvalidateCookies(ScraperConfigurations.StoresJsonUrl);
            }

            // TODO: Report the status as error.
            if (storesToImport != null)
            {
                var storesUrl = storesToImport.Select(store => new Uri(store.Url));
                foreach (var url in storesUrl)
                {
                    await AddAsync(url);
                }
            }
        }
    }
}