using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Catel.Caching;
using Catel.Caching.Policies;
using Serilog;
using YourShipping.Monitor.Server.Extensions;
using YourShipping.Monitor.Server.Helpers;
using YourShipping.Monitor.Server.Models;
using YourShipping.Monitor.Server.Services.Interfaces;

namespace YourShipping.Monitor.Server.Services
{
    public class StoreScraper : IEntityScraper<Store>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Store> cacheStorage;

        private readonly ICookiesSynchronizationService cookiesSynchronizationService;

        private readonly IOfficialStoreInfoService officialStoreInfoService;

        public StoreScraper(
            IBrowsingContext browsingContext,
            ICacheStorage<string, Store> cacheStorage,
            IOfficialStoreInfoService officialStoreInfoService,
            ICookiesSynchronizationService cookiesSynchronizationService)
        {
            this.browsingContext = browsingContext;
            this.cacheStorage = cacheStorage;
            this.officialStoreInfoService = officialStoreInfoService;
            this.cookiesSynchronizationService = cookiesSynchronizationService;
        }

        public async Task<Store> GetAsync(string url, bool force = false, params object[] parameters)
        {
            url = UriHelper.EnsureStoreUrl(url);

            return await cacheStorage.GetFromCacheOrFetchAsync(
                url,
                async () => await GetDirectAsync(url),
                ExpirationPolicy.Duration(ScraperConfigurations.StoreCacheExpiration),
                force);
        }


        private async Task<Store> GetDirectAsync(string storeUrl)
        {
            Log.Information("Scrapping Store from {Url}", storeUrl);

            var storesToImport = await officialStoreInfoService.GetAsync();

            var isStoredClosed = true;
            var requestUri = storeUrl;
            string content = null;
            try
            {
                var httpClient = await cookiesSynchronizationService.CreateHttpClientAsync(storeUrl);

                var httpResponseMessage =
                    await httpClient.GetCaptchaSaveAsync(requestUri);
                if (httpResponseMessage?.Content != null)
                {
                    var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
                    if (requestUriAbsoluteUri.Contains("/SignIn.aspx?ReturnUrl="))
                    {
                        Log.Warning("There is no session available.");
                        cookiesSynchronizationService.InvalidateCookies(storeUrl);
                        
                        return null;
                    }

                    isStoredClosed = requestUriAbsoluteUri.EndsWith("StoreClosed.aspx");
                    if (!isStoredClosed)
                    {
                        content = await httpResponseMessage.Content.ReadAsStringAsync();
                        await cookiesSynchronizationService.SyncCookiesAsync(httpClient, storeUrl);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting Store from '{url}'", storeUrl);
            }

            var storeToImport =
                storesToImport?.FirstOrDefault(s => $"{s.Url.Trim(' ', '/')}/Products?depPid=0" == storeUrl.Trim());
            var storeName = storeToImport?.Name;


            var isAvailable = false;
            var categoriesCount = 0;
            var departmentsCount = 0;
            var hasProductInCart = false;

            if (isStoredClosed)
            {
                Log.Warning("Store '{Name}' with Url '{Url}' is closed", storeName, storeUrl);
            }
            else if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await browsingContext.OpenAsync(req => req.Content(content));

                var isBlocked = document.QuerySelector<IElement>("#notfound > div.notfound > div > h1")?.TextContent
                                == "503";
                if (isBlocked)
                {
                    // TODO: Slow down approach?
                    Log.Error("The  request to store '{Url}' was blocked", storeUrl);
                }
                else
                {
                    var isUserLogged = document.QuerySelector<IElement>("#ctl00_LoginName1") != null;
                    hasProductInCart = document
                        .QuerySelectorAll<IElement>("#ctl00_UpperCartPanel > div > table > tbody > tr > td > a").Any();

                    if (string.IsNullOrWhiteSpace(storeName))
                    {
                        var footerElement = document.QuerySelector<IElement>("#footer > div.container > div > div > p");
                        var uriParts = storeUrl.Split('/');
                        if (uriParts.Length > 3)
                        {
                            storeName = storeUrl.Split('/')[3];
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
                        Log.Warning(
                            "There is no a session open for store '{Store}' with url '{Url}'. Cookies will be invalidated.",
                            storeName,
                            storeUrl);

                        cookiesSynchronizationService.InvalidateCookies(storeUrl);
                    }
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
                    Url = storeUrl,
                    IsAvailable = isAvailable,
                    HasProductsInCart = hasProductInCart,
                    IsEnabled = true
                };

                store.Sha256 = JsonSerializer.Serialize(store).ComputeSha256();
                return store;
            }

            return null;
        }
    }
}