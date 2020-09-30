using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
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
    /// <summary>
    ///     The product reader.
    /// </summary>
    public class ProductScraper : IEntityScraper<Product>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IEntityScraper<Department> _departmentScraper;

        private readonly IEntityScraper<Store> _storeScraper;

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Product> cacheStorage;

        private readonly ICookiesSynchronizationService cookiesSynchronizationService;

        public ProductScraper(
            IBrowsingContext browsingContext,
            IEntityScraper<Store> storeScraper,
            IEntityScraper<Department> departmentScraper,
            ICacheStorage<string, Product> cacheStorage,
            ICookiesSynchronizationService cookiesSynchronizationService)
        {
            this.browsingContext = browsingContext;
            _storeScraper = storeScraper;
            _departmentScraper = departmentScraper;
            this.cacheStorage = cacheStorage;
            this.cookiesSynchronizationService = cookiesSynchronizationService;
        }

        public async Task<Product> GetAsync(string url, bool force = false, params object[] parameters)
        {
            var store = parameters?.OfType<Store>().FirstOrDefault();
            var department = parameters?.OfType<Department>().FirstOrDefault();
            var disabledProducts = parameters?.OfType<ImmutableSortedSet<string>>().FirstOrDefault();


            url = ScrapingUriHelper.EnsureProductUrl(url);

            return await cacheStorage.GetFromCacheOrFetchAsync(
                $"{url}/{store != null}/{department != null}",
                async () => await GetDirectAsync(url, store, department, disabledProducts),
                ExpirationPolicy.Duration(ScraperConfigurations.ProductCacheExpiration),
                force);
        }

        private static bool IsInCart(Product product, IDocument document)
        {
            var querySelectorAll =
                document.QuerySelectorAll<IElement>("#ctl00_UpperCartPanel > div > table > tbody > tr > td > a");
            var any = querySelectorAll.Any(element => product.Url.EndsWith(element.Attributes["href"].Value));
            return any;
        }

        private async Task<Product> GetDirectAsync(
            string url,
            Store parentStore,
            Department parentDepartment,
            ImmutableSortedSet<string> disabledProducts)
        {
            Log.Information("Scrapping Product from {Url}", url);

            var store = parentStore ?? await _storeScraper.GetAsync(url);
            if (store == null || !store.IsAvailable)
            {
                return null;
            }

            var httpClient = await cookiesSynchronizationService.CreateHttpClientAsync(store.Url);

            var department = parentDepartment ?? await _departmentScraper.GetAsync(url, false, store);
            if (department == null || !department.IsAvailable)
            {
                return null;
            }

            var storeName = store.Name;
            var departmentName = department?.Name;
            var departmentCategory = department?.Category;

            var isStoredClosed = true;
            string content = null;
            try
            {
                var httpResponseMessage = await httpClient.CaptchaSaveTaskAsync(async client =>
                    await client.GetAsync(url + $"&requestId={Guid.NewGuid()}"));
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
                Log.Error(e, "Error requesting Department '{Url}'", url);
            }

            if (isStoredClosed)
            {
                Log.Warning("Store '{Name}' with Url '{Url}' is closed", storeName, store.Url);
            }
            else if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await browsingContext.OpenAsync(req => req.Content(content));

                var isBlocked = document.QuerySelector<IElement>("#notfound > div.notfound > div > h1")?.TextContent
                                == "503";
                if (isBlocked)
                {
                    // TODO: Slow down approach?
                    Log.Error("The request to product '{Url}' was blocked", url);
                }
                else
                {
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

                    var mainPanelElement = document.QuerySelector<IElement>("div#mainPanel");
                    if (mainPanelElement != null)
                    {
                        var errorElement = mainPanelElement.QuerySelector<IElement>("#ctl00_cphPage_lblError");
                        if (errorElement != null)
                        {
                            return null;
                        }

                        var missingProductElement = mainPanelElement.QuerySelector<IElement>(
                            "#ctl00_cphPage_formProduct_ctl00_productError_missingProduct");

                        IElement productPriceElement;
                        IElement productNameElement;
                        var isAvailable = missingProductElement == null;
                        if (!isAvailable)
                        {
                            productNameElement = mainPanelElement.QuerySelector<IElement>(
                                "#ctl00_cphPage_UpdatePanel1 > table > tbody > tr:nth-child(4) > td > table > tbody > tr > td:nth-child(5) > table > tbody > tr:nth-child(1) > td.DescriptionValue > span");
                            productPriceElement = mainPanelElement.QuerySelector<IElement>(
                                "#ctl00_cphPage_UpdatePanel1 > table > tbody > tr:nth-child(4) > td > table > tbody > tr > td:nth-child(5) > table > tbody > tr:nth-child(2) > td.PrecioProdList");
                        }
                        else
                        {
                            productNameElement = mainPanelElement.QuerySelector<IElement>(
                                "#ctl00_cphPage_UpdatePanel1 > div > div.product-details.clearfix > div.span5 > div.product-title > h4");

                            productPriceElement = mainPanelElement.QuerySelector<IElement>(
                                "#ctl00_cphPage_UpdatePanel1 > div > div.product-details.clearfix > div.span4 > div.product-set > div.product-price > span");

                            if (productPriceElement == null)
                            {
                                productPriceElement = mainPanelElement.QuerySelector<IElement>(
                                    "#cphPage_UpdatePanel1 > div > div.product-details.clearfix > div.span4 > div.product-set > div.product-price > span");
                            }
                        }

                        var name = productNameElement?.TextContent.Trim();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = mainPanelElement.QuerySelector<IElement>(
                                    "#ctl00_cphPage_UpdatePanel1 > div > div.product-details.clearfix > div.span4 > div.product-set > div.product-info > dl > dd:nth-child(4)")
                                ?.TextContent.Trim();
                        }

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = mainPanelElement.QuerySelector<IElement>(
                                    "#cphPage_UpdatePanel1 > div > div.product-details.clearfix > div.span4 > div.product-set > div.product-info > dl > dd:nth-child(4)")
                                ?.TextContent.Trim();
                        }

                        float price = 0;
                        string currency = null;
                        var priceParts = productPriceElement?.TextContent.Trim().Split(' ');
                        if (priceParts != null && priceParts.Length > 0)
                        {
                            price = float.Parse(priceParts[0].Trim(' ', '$'));
                            currency = priceParts[^1];
                        }

                        var product = new Product
                        {
                            Name = name,
                            Price = price,
                            Currency = currency,
                            Url = url,
                            Store = storeName,
                            Department = departmentName,
                            DepartmentCategory = departmentCategory,
                            IsAvailable = isAvailable,
                            IsEnabled = true
                        };

                        // This can be done in other place?
                        if (isUserLogged && (disabledProducts == null || !disabledProducts.Contains(url)))
                        {
                            await TryAddProductToShoppingCart(httpClient, product, document);
                        }

                        if (!isUserLogged)
                        {
                            Log.Warning(
                                "There is no a session open for trying to add the product '{ProductName}' to the shopping chart on store '{StoreName}' with url '{Url}'. Cookies will be invalidated.",
                                product.Name,
                                storeName,
                                store.Url);

                            cookiesSynchronizationService.InvalidateCookies(store.Url);
                        }

                        product.Sha256 = JsonSerializer.Serialize(product).ComputeSHA256();

                        return product;
                    }
                }
            }

            return null;
        }

        private async Task TryAddProductToShoppingCart(HttpClient httpClient, Product product, IDocument document)
        {
            try
            {
                if (IsInCart(product, document))
                {
                    product.IsInCart = true;

                    Log.Information(
                        "Product '{ProductName}' is already in the shopping cart on the store '{StoreName}'",
                        product.Name,
                        product.Store);
                }
                else if (product.IsAvailable)
                {
                    Log.Information(
                        "Trying to add the product '{ProductName}' to the cart on the store '{StoreName}'",
                        product.Name,
                        product.Store);
                    var parameters = new Dictionary<string, string>
                    {
                        {
                            "ctl00$ScriptManager1",
                            "ctl00$cphPage$UpdatePanel1|ctl00$cphPage$formProduct$ctl00$productDetail$btnAddCar"
                        },
                        {
                            "__EVENTTARGET",
                            "ctl00$cphPage$formProduct$ctl00$productDetail$btnAddCar"
                        },
                        {"__EVENTARGUMENT", string.Empty},
                        {"__LASTFOCUS", string.Empty},
                        {
                            "PageLoadedHiddenTxtBox",
                            document.QuerySelector<IElement>("#PageLoadedHiddenTxtBox")
                                ?.Attributes["value"]?.Value
                        },
                        {
                            "ctl00_cphPage_formProduct_ctl00_productDetail_DetailTabs_ClientState",
                            document.QuerySelector<IElement>(
                                    "#ctl00_cphPage_formProduct_ctl00_productDetail_DetailTabs_ClientState")
                                ?.Attributes["value"]?.Value
                        },
                        {
                            "__VIEWSTATE",
                            document.QuerySelector<IElement>("#__VIEWSTATE")?.Attributes["value"]
                                ?.Value
                        },
                        {
                            "__EVENTVALIDATION",
                            document.QuerySelector<IElement>("#__EVENTVALIDATION")
                                ?.Attributes["value"]?.Value
                        },
                        {
                            "ctl00_cphPage_formProduct_ctl00_productDetail_pdtRating_RatingExtender_ClientState",
                            document.QuerySelector<IElement>(
                                    "#ctl00_cphPage_formProduct_ctl00_productDetail_pdtRating_RatingExtender_ClientState")
                                ?.Attributes["value"]?.Value
                        },
                        {
                            "ctl00_cphPage_formProduct_ctl00_productDetail_txtCount",
                            document.QuerySelector<IElement>(
                                    "#ctl00_cphPage_formProduct_ctl00_productDetail_txtCount")
                                ?.Attributes["value"]?.Value
                        },
                        {"ctl00$taxes$listCountries", "54"},
                        {"Language", "es-MX"},
                        {"CurrentLanguage", "es-MX"},
                        {"Currency", product.Currency},
                        {"__ASYNCPOST", "true"}
                    };

                    var httpResponseMessage = await httpClient.CaptchaSaveTaskAsync(async client =>
                        await httpClient.PostAsync(
                            product.Url,
                            new FormUrlEncodedContent(parameters)));

                    httpResponseMessage.EnsureSuccessStatusCode();

                    var content = await httpClient.GetStringAsync(product.Url);
                    var responseDocument = await browsingContext.OpenAsync(req => req.Content(content));
                    if (IsInCart(product, responseDocument))
                    {
                        product.IsInCart = true;
                        Log.Information(
                            "Product '{ProductName}' was added to the cart on the store '{StoreName}'",
                            product.Name,
                            product.Store);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error adding product '{ProductName}' to the shopping cart", product.Name);
            }
        }
    }
}