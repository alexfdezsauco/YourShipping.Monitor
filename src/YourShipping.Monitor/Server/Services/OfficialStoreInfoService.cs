namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Serilog;

    using YourShipping.Monitor.Server.Models;

    public class OfficialStoreInfoService : IOfficialStoreInfoService
    {
        private readonly ICookiesSynchronizationService cookiesSynchronizationService;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private DateTime? setTime;

        private OfficialStoreInfo[] storesToImport;

        public OfficialStoreInfoService(ICookiesSynchronizationService cookiesSynchronizationService)
        {
            this.cookiesSynchronizationService = cookiesSynchronizationService;
        }

        public async Task<OfficialStoreInfo[]> GetAsync()
        {
            await this.semaphoreSlim.WaitAsync();

            if (this.setTime.HasValue && DateTime.Now.Subtract(this.setTime.Value).TotalSeconds > 30)
            {
                this.storesToImport = null;
            }

            if (this.storesToImport == null)
            {
                try
                {
                    var httpClient = await this.cookiesSynchronizationService.CreateHttpClientAsync(ScraperConfigurations.StoresJsonUrl);
                    this.storesToImport =
                        await httpClient.GetFromJsonAsync<OfficialStoreInfo[]>(ScraperConfigurations.StoresJsonUrl);
                    await this.cookiesSynchronizationService.SyncCookiesAsync(
                        httpClient,
                        ScraperConfigurations.StoresJsonUrl);
                    this.setTime = DateTime.Now;
                }
                catch (Exception e)
                {
                    Log.Error(
                        e,
                        "Error requesting '{Url}'. Cookies will be invalidated.",
                        ScraperConfigurations.StoresJsonUrl);
                    this.cookiesSynchronizationService.InvalidateCookies(ScraperConfigurations.StoresJsonUrl);
                }
            }

            var officialStoreInfos = this.storesToImport;

            this.semaphoreSlim.Release();

            return officialStoreInfos;
        }
    }
}