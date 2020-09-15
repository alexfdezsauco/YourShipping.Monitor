namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Js;

    using Catel.Caching;
    using Catel.Caching.Policies;

    using Newtonsoft.Json;

    using Serilog;
    using Serilog.Core;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    using JsonSerializer = System.Text.Json.JsonSerializer;

    public class StoreScrapper : IEntityScrapper<Store>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Store> cacheStorage;

        private readonly ICookiesSynchronizationService cookiesSynchronizationService;

        private readonly HttpClient httpClient;

        public StoreScrapper(
            IBrowsingContext browsingContext,
            ICacheStorage<string, Store> cacheStorage,
            ICookiesSynchronizationService cookiesSynchronizationService,
            HttpClient httpClient)
        {
            this.browsingContext = browsingContext;
            this.cacheStorage = cacheStorage;
            this.cookiesSynchronizationService = cookiesSynchronizationService;
            this.httpClient = httpClient;
        }

        public async Task<Store> GetAsync(string url, bool force = false, params object[] parameters)
        {
            var uri = new Uri(url);
            url =
                $"{uri.Scheme}://{uri.DnsSafeHost}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : string.Empty)}/{uri.Segments[1].Trim(' ', '/')}/Products?depPid=0";

            return await this.cacheStorage.GetFromCacheOrFetchAsync(
                       url,
                       async () => await this.GetDirectAsync(url),
                       ExpirationPolicy.Duration(ScrappingConfiguration.StoreCacheExpiration),
                       force);
        }

        private async Task<Store> GetDirectAsync(string url)
        {
            Log.Information("Scrapping Store from {Url}", url);

            OfficialStoreInfo[] storesToImport = null;
            try
            {
                storesToImport = await this.httpClient.GetFromJsonAsync<OfficialStoreInfo[]>("https://www.tuenvio.cu/stores.json");

                var httpClientHandler = this.httpClient.GetHttpClientHandler();
                this.cookiesSynchronizationService.SyncCookies(httpClientHandler.CookieContainer);
            }
            catch (Exception e)
            {
                this.cookiesSynchronizationService.InvalidateCookies();
                Log.Error(e, "Error requesting stores.json");
            }

            var requestUri = url;
            string content = null;
            try
            {
                content = await this.httpClient.GetStringAsync(requestUri);

                var httpClientHandler = this.httpClient.GetHttpClientHandler();
                this.cookiesSynchronizationService.SyncCookies(httpClientHandler.CookieContainer);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting Store from '{url}'", url);
            }

            var storeToImport = storesToImport?.FirstOrDefault(s => $"{s.Url.Trim()}/Products?depPid=0" == url.Trim());
            var storeName = storeToImport?.Name;
            var isAvailable = false;
            var categoriesCount = 0;
            var departmentsCount = 0;

            if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await this.browsingContext.OpenAsync(req => req.Content(content));

                var isUserLogged = document.QuerySelector<IElement>("#ctl00_LoginName1") != null;

                if (string.IsNullOrWhiteSpace(storeName))
                {
                    var footerElement = document.QuerySelector<IElement>("#footer > div.container > div > div > p");
                    var uriParts = url.Split('/');
                    if (uriParts.Length > 3)
                    {
                        storeName = url.Split('/')[3];
                    }

                    if (footerElement != null)
                    {
                        var footerElementTextParts = footerElement.TextContent.Split('•');
                        if (footerElementTextParts.Length > 0)
                        {
                            storeName = footerElementTextParts[^1].Trim();
                            if (storeName.StartsWith(StorePrefix, StringComparison.CurrentCultureIgnoreCase)
                                && storeName.Length > StorePrefix.Length)
                            {
                                storeName = storeName.Substring(StorePrefix.Length - 1);
                            }
                        }
                    }
                }

                var mainNavElement = document.QuerySelector<IElement>("#mainContainer > header > div.mainNav");
                if (mainNavElement != null)
                {
                    isAvailable = true;
                    var elements = mainNavElement.QuerySelectorAll<IElement>("div > div > ul > li").ToList();
                    foreach (var element in elements)
                    {
                        if (!element.InnerHtml.Contains("<i class=\"icon-home\"></i>"))
                        {
                            categoriesCount++;
                            var querySelector = element.QuerySelector<IElement>("a");
                            if (querySelector != null)
                            {
                                querySelector.QuerySelector("i")?.Remove();
                                var name = querySelector.TextContent;
                            }

                            var departmentsElementSelector =
                                element.QuerySelectorAll<IElement>("div > ul > li").ToList();
                            departmentsCount += departmentsElementSelector.Count;
                        }
                    }
                }

                if (!isUserLogged)
                {
                    this.cookiesSynchronizationService.InvalidateCookies();
                }
            }

            if (!string.IsNullOrEmpty(storeName))
            {
                var store = new Store
                                {
                                    Name = storeName,
                                    DepartmentsCount = departmentsCount,
                                    CategoriesCount = categoriesCount,
                                    Province = storeToImport?.Province,
                                    Url = url,
                                    IsAvailable = isAvailable,
                                    IsEnabled = true
                                };

                store.Sha256 = JsonSerializer.Serialize(store).ComputeSHA256();
                return store;
            }

            return null;
        }
    }
}