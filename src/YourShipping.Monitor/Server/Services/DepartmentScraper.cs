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

        private readonly Regex anchorParameterNameRegex = new Regex(
            @"WebForm_PostBackOptions\(""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Department> cacheStorage;

        private readonly ICookiesAwareHttpClientFactory cookiesAwareHttpClientFactory;

        private readonly IEntityScraper<Store> storeScraper;

        public DepartmentScraper(
            IBrowsingContext browsingContext,
            IEntityScraper<Store> storeScraper,
            ICacheStorage<string, Department> cacheStorage,
            ICookiesAwareHttpClientFactory cookiesAwareHttpClientFactory)
        {
            this.browsingContext = browsingContext;
            this.storeScraper = storeScraper;
            this.cacheStorage = cacheStorage;
            this.cookiesAwareHttpClientFactory = cookiesAwareHttpClientFactory;
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

            var store = parentStore ?? await this.storeScraper.GetAsync(url);
            if (store == null || !store.IsAvailable)
            {
                return null;
            }

            var storeName = store?.Name;
            Department department = null;
            var currencies = new[] { "CUP", "CUC" };
            var i = 0;
            Department bestScrapedDepartment = null;

            // TODO: Review this because probably is no longer required.
            while (i < currencies.Length && (department == null || department.ProductsCount == 0))
            {
                var currency = currencies[i];
                var requestUris = new[] { url + "&page=0", url };

                var j = 0;
                while (j < requestUris.Length && (department == null || department.ProductsCount == 0))
                {
                    var requestUri = requestUris[j];
                    string content = null;
                    try
                    {
                        var nameValueCollection = new Dictionary<string, string> { { "Currency", currency } };
                        var formUrlEncodedContent = new FormUrlEncodedContent(nameValueCollection);
                        var httpClient = await this.cookiesAwareHttpClientFactory.CreateHttpClientAsync(store.Url);

                        httpClient.DefaultRequestHeaders.Referrer = new Uri(store.Url);
                        var httpResponseMessage = await httpClient.PostCaptchaSaveAsync(requestUri, formUrlEncodedContent);
                        if (httpResponseMessage?.Content != null)
                        {
                            if (httpResponseMessage.IsSignInRedirectResponse())
                            {
                                Log.Warning("There is no session available.");
                                this.cookiesAwareHttpClientFactory.InvalidateCookies(store.Url);
                                return null;
                            }

                            if (httpResponseMessage.IsStoreClosedRedirectResponse())
                            {
                                Log.Warning("Store '{Name}' with Url '{Url}' is closed", storeName, store.Url);
                                return null;
                            }

                            content = await httpResponseMessage.Content.ReadAsStringAsync();
                            await this.cookiesAwareHttpClientFactory.SyncCookiesAsync(store.Url, httpClient);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error requesting Department '{Url}'", url);
                    }

                    if (!string.IsNullOrEmpty(content))
                    {
                        var document = await this.browsingContext.OpenAsync(req => req.Content(content));
                        var isBlocked = document.QuerySelector<IElement>("#notfound > div.notfound > div > h1")?.TextContent == "503";
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
                                                var product = await this.TryAddProductToShoppingCart(
                                                                  department,
                                                                  document,
                                                                  productElement,
                                                                  disabledProducts);
                                                if (product != null)
                                                {
                                                    lock (department)
                                                    {
                                                        department.Products.Add(product.Url, product);
                                                    }
                                                }
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
                                this.cookiesAwareHttpClientFactory.InvalidateCookies(store.Url);
                            }
                        }
                    }

                    j++;
                }

                i++;
            }

            return bestScrapedDepartment;
        }

        private async Task<Product> TryAddProductToShoppingCart(
            Department department,
            IDocument document,
            IElement productElement,
            ImmutableSortedSet<string> disabledProducts)
        {
            var storeUrl = UriHelper.EnsureStoreUrl(department.Url);
            var httpClient = await this.cookiesAwareHttpClientFactory.CreateHttpClientAsync(storeUrl);

            var productNameAnchor = productElement.QuerySelector<IElement>("div.thumbSetting > div.thumbTitle > a");
            var productUrl = productNameAnchor.Attributes["href"]?.Value;

            productUrl = UriHelper.EnsureProductUrl(productUrl);
            var productName = productNameAnchor.Text();
            if (disabledProducts != null && disabledProducts.Contains(productUrl))
            {
                Log.Information(
                    "Found product {Product} in department '{DepartmentName}' but was ignored.",
                    productName,
                    department.Name);
                return null;
            }

            Log.Information("Found product {Product} in department '{DepartmentName}'", productName, department.Name);

            var countInput = productElement.QuerySelector<IElement>("div.thumbSetting > div.thumbButtons > input");
            var addToCartButtonAnchor =
                productElement.QuerySelector<IElement>("div.thumbSetting > div.thumbButtons > a:nth-child(2)");
            try
            {
                var anchorValue = addToCartButtonAnchor.Attributes["href"].Value;
                var match = this.anchorParameterNameRegex.Match(anchorValue);
                Log.Information(
                    "Try to add  product {Product} in department '{DepartmentName}' to the shopping cart.",
                    productName,
                    department.Name);

                var anchorParameterName = match.Groups[1].Value;
                var inputParameterName = countInput.Attributes["name"].Value;
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
                if (httpResponseMessage == null)
                {
                    return null;
                }

                httpResponseMessage.EnsureSuccessStatusCode();
                await this.cookiesAwareHttpClientFactory.SyncCookiesAsync(storeUrl, httpClient);

                // var content = await httpResponseMessage.Content.ReadAsStringAsync();
                // var storeSlug = UriHelper.GetStoreSlug(storeUrl);
                // if (!Directory.Exists($"products/{storeSlug}"))
                // {
                // Directory.CreateDirectory($"products/{storeSlug}");
                // }

                // File.WriteAllText($"products/{storeSlug}/{productName.ComputeSha256()}.html", content);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error adding product '{ProductName}' to the shopping cart", productName);
            }

            float price = 0;
            var currency = string.Empty;

            var priceSpan = productElement.QuerySelector<IElement>("div.thumbSetting > div.thumbPrice > span");
            var priceParts = priceSpan?.Text()?.Trim()?.Split(' ');
            if (priceParts != null && priceParts.Length == 2)
            {
                float.TryParse(priceParts[0], out price);
                currency = priceParts[1];
            }

            var imageObject = productElement.QuerySelector<IElement>("div.thumbnail > a > object");
            var imageUrl = imageObject?.Attributes["data"]?.Value;

            var product = new Product
                              {
                                  Name = productName,
                                  Url = productUrl,
                                  ImageUrl = imageUrl,
                                  IsAvailable = true,
                                  DepartmentCategory = department.Category,
                                  Department = department.Name,
                                  IsEnabled = true,
                                  Price = price,
                                  Currency = currency
                              };

            return product;
        }
    }
}