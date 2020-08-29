namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using Catel.Caching;
    using Catel.Caching.Policies;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    /// <summary>
    ///     The product reader.
    /// </summary>
    public class ProductScrapper : IEntityScrapper<Product>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Product> cacheStorage;


        private readonly IEntityScrapper<Department> departmentScrapper;


        private readonly IEntityScrapper<Store> storeScrapper;

        private readonly HttpClient webPageHttpClient;

        public ProductScrapper(
            IBrowsingContext browsingContext,
            IEntityScrapper<Store> storeScrapper,
            IEntityScrapper<Department> departmentScrapper,
            ICacheStorage<string, Product> cacheStorage,
            HttpClient webPageHttpClient)
        {
            this.browsingContext = browsingContext;
            this.storeScrapper = storeScrapper;
            this.departmentScrapper = departmentScrapper;
            this.cacheStorage = cacheStorage;
           this.webPageHttpClient = webPageHttpClient;
        }

        public async Task<Product> GetAsync(
            string url,
            bool force = false,
            params object[] parameters)
        {
            var store = parameters?.OfType<Store>().FirstOrDefault();
            var department = parameters?.OfType<Department>().FirstOrDefault();

            url = Regex.Replace(
                url,
                @"(&?)(page=\d+(&?)|img=\d+(&?))",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ');

            return await this.cacheStorage.GetFromCacheOrFetchAsync(
                       $"{url}/{store != null}/{department != null}",
                       async () => await this.GetDirectAsync(url, store, department),
                       ExpirationPolicy.Duration(ScrappingConfiguration.Expiration),
                       force);
        }

        private static bool IsInCart(Product product, IDocument document)
        {
            var querySelectorAll =
                document.QuerySelectorAll<IElement>("#ctl00_UpperCartPanel > div > table > tbody > tr > td > a");
            var any = querySelectorAll.Any(element => product.Url.EndsWith(element.Attributes["href"].Value));
            return any;
        }

        private async Task<Product> GetDirectAsync(string url, Store parentStore, Department parentDepartment)
        {
            Log.Information("Scrapping Product from {Url}", url);

            var store = parentStore ?? await this.storeScrapper.GetAsync(url);
            if (store == null)
            {
                return null;
            }

            var department = parentDepartment ?? await this.departmentScrapper.GetAsync(url, false, store);

            // if (department == null)
            // {
            // return null;
            // }
            var storeName = store.Name;
            var departmentName = department?.Name;
            var departmentCategory = department?.Category;

            string content = null;

            try
            {
                content = await this.webPageHttpClient.GetStringAsync(url);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting Department '{url}'", url);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await this.browsingContext.OpenAsync(req => req.Content(content));
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

                    await this.TryAddProductToShoppingCart(product, document);

                    product.Sha256 = JsonSerializer.Serialize(product).ComputeSHA256();

                    return product;
                }
            }

            return null;
        }

        private async Task TryAddProductToShoppingCart(Product product, IDocument document)
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
                else
                {
                    // TODO: Check if the product is already in the cart.
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
                                             { "__EVENTARGUMENT", string.Empty },
                                             { "__LASTFOCUS", string.Empty },
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
                                             { "ctl00$taxes$listCountries", "54" },
                                             { "Language", "es-MX" },
                                             { "CurrentLanguage", "es-MX" },
                                             { "Currency", product.Currency },
                                             { "__ASYNCPOST", "true" }
                                         };

                    var httpResponseMessage = await this.webPageHttpClient.PostAsync(
                                                  product.Url,
                                                  new FormUrlEncodedContent(parameters));

                    httpResponseMessage.EnsureSuccessStatusCode();

                    var content = await httpResponseMessage.Content.ReadAsStringAsync();
                    var responseDocument = await this.browsingContext.OpenAsync(req => req.Content(content));

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