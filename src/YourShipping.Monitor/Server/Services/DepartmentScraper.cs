namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using Catel.Caching;
    using Catel.Caching.Policies;

    using Dasync.Collections;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class DepartmentScraper : IEntityScraper<Department>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IEntityScraper<Store> _storeScraper;

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Department> cacheStorage;

        private readonly ICookiesSynchronizationService cookiesSynchronizationService;

        private readonly IServiceProvider serviceProvider;

        public DepartmentScraper(
            IBrowsingContext browsingContext,
            IEntityScraper<Store> storeScraper,
            ICacheStorage<string, Department> cacheStorage,
            ICookiesSynchronizationService cookiesSynchronizationService,
            IServiceProvider serviceProvider)
        {
            this.browsingContext = browsingContext;
            this._storeScraper = storeScraper;
            this.cacheStorage = cacheStorage;
            this.cookiesSynchronizationService = cookiesSynchronizationService;
            this.serviceProvider = serviceProvider;
        }

        public async Task<Department> GetAsync(string url, bool force = false, params object[] parameters)
        {
            var store = parameters?.OfType<Store>().FirstOrDefault();
            var disabledProducts = parameters?.OfType<ImmutableSortedSet<string>>().FirstOrDefault();
            url = UriHelper.EnsureDepartmentUrl(url);

            if (!Regex.IsMatch(url, @"depPid=\d+", RegexOptions.IgnoreCase))
            {
                return null;
            }

            return await this.cacheStorage.GetFromCacheOrFetchAsync(
                       $"{url}/{store != null}",
                       async () => await this.GetDirectAsync(url, store, disabledProducts),
                       ExpirationPolicy.Duration(ScraperConfigurations.DepartmentCacheExpiration),
                       force);
        }

        private async Task<Department> GetDirectAsync(
            string url,
            Store parentStore,
            ImmutableSortedSet<string> disabledProducts)
        {
            Log.Information("Scrapping Department from {Url}", url);

            var store = parentStore ?? await this._storeScraper.GetAsync(url);
            if (store == null || !store.IsAvailable)
            {
                return null;
            }

            var storeName = store?.Name;
            Department department = null;
            var currencies = new[] { "CUP", "CUC" };
            var i = 0;
            var isStoredClosed = false;
            Department bestScrapedDepartment = null;

            while (!isStoredClosed && i < currencies.Length && (department == null || department.ProductsCount == 0))
            {
                var currency = currencies[i];
                var requestUris = new[] { url + "&page=0", url };

                var j = 0;
                while (!isStoredClosed && j < requestUris.Length
                                       && (department == null || department.ProductsCount == 0))
                {
                    var requestUri = requestUris[j];
                    string content = null;
                    try
                    {
                        var nameValueCollection = new Dictionary<string, string> { { "Currency", currency } };
                        var formUrlEncodedContent = new FormUrlEncodedContent(nameValueCollection);
                        var httpClient = await this.cookiesSynchronizationService.CreateHttpClientAsync(store.Url);

                        httpClient.DefaultRequestHeaders.Referrer = new Uri(store.Url);
                        var httpResponseMessage =
                            await httpClient.PostCaptchaSaveAsync(requestUri, formUrlEncodedContent);
                        if (httpResponseMessage?.Content != null)
                        {
                            var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
                            if (requestUriAbsoluteUri.Contains("/SignIn.aspx?ReturnUrl="))
                            {
                                Log.Warning("There is no session available.");
                                this.cookiesSynchronizationService.InvalidateCookies(store.Url);

                                return null;
                            }

                            isStoredClosed = requestUriAbsoluteUri.EndsWith("StoreClosed.aspx");
                            if (!isStoredClosed)
                            {
                                content = await httpResponseMessage.Content.ReadAsStringAsync();
                                await this.cookiesSynchronizationService.SyncCookiesAsync(httpClient, store.Url);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error requesting Department '{url}'", url);
                    }

                    if (isStoredClosed)
                    {
                        Log.Warning("Store '{Name}' with Url '{Url}' is closed", storeName, store.Url);
                    }
                    else if (!string.IsNullOrEmpty(content))
                    {
                        var document = await this.browsingContext.OpenAsync(req => req.Content(content));

                        var isBlocked = document.QuerySelector<IElement>("#notfound > div.notfound > div > h1")
                                            ?.TextContent == "503";
                        if (isBlocked)
                        {
                            // TODO: Slow down approach?
                            Log.Error("The request to department '{Url}' was blocked", url);
                        }
                        else
                        {
                            var isUserLogged = document.QuerySelector<IElement>("#ctl00_LoginName1") != null;
                            if (string.IsNullOrWhiteSpace(storeName))
                            {
                                var footerElement =
                                    document.QuerySelector<IElement>("#footer > div.container > div > div > p");
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

                            var mainPanelElement = document.QuerySelector<IElement>("div#mainPanel");
                            if (mainPanelElement != null)
                            {
                                var filterElement =
                                    mainPanelElement?.QuerySelector<IElement>("div.productFilter.clearfix");
                                filterElement?.Remove();

                                var departmentElements = mainPanelElement
                                    .QuerySelectorAll<IElement>("#mainPanel > span > a").ToList();

                                if (departmentElements.Count > 2)
                                {
                                    var departmentCategory = departmentElements[^2].TextContent.Trim();
                                    var departmentName = departmentElements[^1].TextContent.Trim();

                                    if (!string.IsNullOrWhiteSpace(departmentName)
                                        && !string.IsNullOrWhiteSpace(departmentCategory))
                                    {
                                        department = new Department
                                                         {
                                                             Url = url,
                                                             Name = departmentName,
                                                             Category = departmentCategory,
                                                             Store = storeName,
                                                             IsAvailable = true,
                                                             IsEnabled = true
                                                         };
                                    }
                                }

                                if (department != null)
                                {
                                    var productElements = document.QuerySelectorAll<IElement>("li.span3.clearfix")
                                        .ToList();

                                    await productElements.ParallelForEachAsync(
                                        async productElement =>
                                            {
                                                var httpClient =
                                                    await this.cookiesSynchronizationService.CreateHttpClientAsync(
                                                        store.Url);

                                                // TODO: Improve this if it worked
                                                await this.TryAddProductToShoppingCart(
                                                    httpClient,
                                                    document,
                                                    productElement,
                                                    department);

                                                await this.cookiesSynchronizationService.SyncCookiesAsync(
                                                    httpClient,
                                                    store.Url);
                                            });

                                    // TODO: Improve this?
                                    department.ProductsCount = department.Products.Count;
                                    department.Sha256 = JsonSerializer.Serialize(department).ComputeSha256();
                                    bestScrapedDepartment = department;
                                }
                            }

                            if (!isUserLogged)
                            {
                                Log.Warning(
                                    "There is no a session open for store '{Store}' with url '{Url}'. Cookies will be invalidated.",
                                    storeName,
                                    store.Url);
                                this.cookiesSynchronizationService.InvalidateCookies(store.Url);
                            }
                        }
                    }

                    j++;
                }

                i++;
            }

            return bestScrapedDepartment;
        }

        private async Task TryAddProductToShoppingCart(
            HttpClient httpClient,
            IDocument document,
            IElement productElement,
            Department department)
        {
            var element = productElement.QuerySelector<IElement>("div.thumbSetting > div.thumbTitle > a");
            var productName = element.Text();
            var input = element.QuerySelector<IElement>("div.thumbSetting > div.thumbButtons > input");
            var anchor = element.QuerySelector<IElement>("div.thumbSetting > div.thumbButtons > a:nth-child(2)");
            
            Log.Information("Found product {Product} in department '{DepartmentName}'", productName, department.Name);

            try
            {
                var anchorParameterName = anchor.Id.Replace("_", "$");
                var inputParameterName = input.Attributes["name"].Value;
                var parameters = new Dictionary<string, string>
                                     {
                                         {
                                             "ctl00$ScriptManager1",
                                             $"ctl00_cphPage_productsControl_UpdatePanel1|{anchorParameterName}"
                                         },
                                         { "__EVENTTARGET", $"{anchorParameterName}" },
                                         { "__EVENTARGUMENT", string.Empty },
                                         { "__LASTFOCUS", string.Empty },
                                         {
                                             "PageLoadedHiddenTxtBox",
                                             document.QuerySelector<IElement>("#PageLoadedHiddenTxtBox")
                                                 ?.Attributes["value"]?.Value
                                         },
                                         {
                                             "__VIEWSTATE",
                                             document.QuerySelector<IElement>("#__VIEWSTATE")?.Attributes["value"]
                                                 ?.Value
                                         },
                                         {
                                             "__EVENTVALIDATION",
                                             document.QuerySelector<IElement>("#__EVENTVALIDATION")?.Attributes["value"]
                                                 ?.Value
                                         },
                                         { inputParameterName, "1" },
                                         { "ctl00$taxes$listCountries", "54" },
                                         { "Language", "es-MX" },
                                         { "CurrentLanguage", "es-MX" },
                                         { "Currency", string.Empty },
                                         { "__ASYNCPOST", "true" }
                                     };

                httpClient.DefaultRequestHeaders.Referrer = new Uri(department.Url + "&page=0");
                var httpResponseMessage = await httpClient.FormPostCaptchaSaveAsync(department.Url, parameters);
                if (httpResponseMessage?.Content != null)
                {
                    var content = await httpResponseMessage.Content.ReadAsStringAsync();
                    await this.browsingContext.OpenAsync(req => req.Content(content));
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error adding product '{ProductName}' to the shopping cart", productName);
            }
        }
    }
}