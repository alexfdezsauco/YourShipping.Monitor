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
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using YourShipping.Monitor.Server.Extensions;
using YourShipping.Monitor.Server.Helpers;
using YourShipping.Monitor.Server.Models;
using YourShipping.Monitor.Server.Services.Interfaces;

namespace YourShipping.Monitor.Server.Services
{
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
            _storeScraper = storeScraper;
            this.cacheStorage = cacheStorage;
            this.cookiesSynchronizationService = cookiesSynchronizationService;
            this.serviceProvider = serviceProvider;
        }

        public async Task<Department> GetAsync(string url, bool force = false, params object[] parameters)
        {
            var store = parameters?.OfType<Store>().FirstOrDefault();
            var disabledProducts = parameters?.OfType<ImmutableSortedSet<string>>().FirstOrDefault();
            url = ScrapingUriHelper.EnsureDepartmentUrl(url);

            if (!Regex.IsMatch(url, @"depPid=\d+", RegexOptions.IgnoreCase))
            {
                return null;
            }

            return await cacheStorage.GetFromCacheOrFetchAsync(
                $"{url}/{store != null}",
                async () => await GetDirectAsync(url, store, disabledProducts),
                ExpirationPolicy.Duration(ScraperConfigurations.DepartmentCacheExpiration),
                force);
        }

        private async Task<Department> GetDirectAsync(
            string url,
            Store parentStore,
            ImmutableSortedSet<string> disabledProducts)
        {
            Log.Information("Scrapping Department from {Url}", url);

            var store = parentStore ?? await _storeScraper.GetAsync(url);
            if (store == null || !store.IsAvailable)
            {
                return null;
            }

            var storeName = store?.Name;
            Department department = null;
            var currencies = new[] {"CUP", "CUC"};
            var i = 0;
            var isStoredClosed = false;

            while (!isStoredClosed && i < currencies.Length && (department == null || department.ProductsCount == 0))
            {
                var currency = currencies[i];
                var requestUris = new[] {url + "&page=0", url};

                var j = 0;
                while (!isStoredClosed && j < requestUris.Length && (department == null || department.ProductsCount == 0))
                {
                    var requestUri = requestUris[j];
                    string content = null;
                    try
                    {
                        var httpClient = await cookiesSynchronizationService.CreateHttpClientAsync(store.Url);
                        var httpResponseMessage = await httpClient.CaptchaSaveTaskAsync(async client =>
                        {
                            var nameValueCollection = new Dictionary<string, string> {{"Currency", currency}};
                            var formUrlEncodedContent = new FormUrlEncodedContent(nameValueCollection);
                            return await client.PostAsync(requestUri + $"&requestId={Guid.NewGuid()}", formUrlEncodedContent);
                        });

                        var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
                        isStoredClosed = requestUriAbsoluteUri.EndsWith("StoreClosed.aspx");
                        if (!isStoredClosed)
                        {
                            content = await httpResponseMessage.Content.ReadAsStringAsync();
                            await cookiesSynchronizationService.SyncCookiesAsync(httpClient, store.Url);
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
                        var document = await browsingContext.OpenAsync(req => req.Content(content));

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
                                else if (url.Contains("/Search.aspx?keywords="))
                                {
                                    var s = url.Split("?")[1];
                                    var parameters = s.Split("&").ToDictionary(
                                        s1 => s1.Split("=")[0],
                                        s2 => s2.Split("=")[1]);
                                    department = new Department
                                    {
                                        Url = url,
                                        Name = "Search",
                                        Category = "Keywords: " + parameters["keywords"],
                                        Store = storeName,
                                        IsAvailable = true,
                                        IsEnabled = true
                                    };
                                }

                                if (department != null)
                                {
                                    var productElements = mainPanelElement
                                        .QuerySelectorAll<IElement>("li.span3.clearfix").ToList();
                                    var productsCount = 0;
                                    var baseUrl = Regex.Replace(
                                        url,
                                        "/Search[.]aspx[^/]+",
                                        string.Empty,
                                        RegexOptions.IgnoreCase);
                                    baseUrl = Regex.Replace(
                                        baseUrl,
                                        "/(Products|Item)[?]depPid=\\d+",
                                        string.Empty,
                                        RegexOptions.Singleline | RegexOptions.IgnoreCase);

                                    await productElements.ParallelForEachAsync(
                                        async productElement =>
                                        {
                                            var productScrapper = serviceProvider
                                                .GetService<IEntityScraper<Product>>();
                                            var element = productElement.QuerySelector<IElement>("a");
                                            var elementAttribute = element.Attributes["href"];

                                            var productUrl = Regex.Replace(
                                                $"{baseUrl}/{elementAttribute.Value}",
                                                @"(&?)(page=\d+(&?)|img=\d+(&?))",
                                                string.Empty,
                                                RegexOptions.IgnoreCase).Trim(' ');

                                            var product = await productScrapper.GetAsync(
                                                productUrl,
                                                disabledProducts == null
                                                || !disabledProducts.Contains(
                                                    productUrl), // Why was in false.
                                                store,
                                                department,
                                                disabledProducts);

                                            if (product != null && product.IsAvailable)
                                            {
                                                lock (department)
                                                {
                                                    department.Products.Add(product.Url, product);
                                                    productsCount++;
                                                }
                                            }
                                        });

                                    department.ProductsCount = productsCount;
                                    department.Sha256 = JsonSerializer.Serialize(department).ComputeSHA256();
                                }
                            }

                            if (!isUserLogged)
                            {
                                Log.Warning(
                                    "There is no a session open for store '{Store}' with url '{Url}'. Cookies will be invalidated.",
                                    storeName,
                                    store.Url);
                                cookiesSynchronizationService.InvalidateCookies(store.Url);
                            }
                        }
                    }

                    j++;
                }

                i++;
            }

            return department;
        }
    }
}