namespace YourShipping.Monitor.Server.Services
{
    using System;
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

        private readonly IEntityScrapper<Store> storeScrapper;

        private readonly IEntityScrapper<Department> departmentScrapper;

        public ProductScrapper(
            IBrowsingContext browsingContext,
            IEntityScrapper<Store> storeScrapper,
            IEntityScrapper<Department> departmentScrapper,
            ICacheStorage<string, Product> cacheStorage)
        {
            this.browsingContext = browsingContext;
            this.storeScrapper = storeScrapper;
            this.departmentScrapper = departmentScrapper;
            this.cacheStorage = cacheStorage;
        }

        public async Task<Product> GetAsync(string url, bool force = false)
        {
            url = Regex.Replace(url, @"(&?)(page=\d+(&?)|img=\d+(&?))", string.Empty, RegexOptions.IgnoreCase).Trim(' ');
            return await this.cacheStorage.GetFromCacheOrFetchAsync(url, () => this.GetDirectAsync(url), ExpirationPolicy.Duration(ScrappingConfiguration.Expiration), force);
        }

        private async Task<Product> GetDirectAsync(string url)
        {
            Log.Information("Scrapping Product from {Url}", url);

            var store = await this.storeScrapper.GetAsync(url);
            if (store == null)
            {
                return null;
            }

            var department = await this.departmentScrapper.GetAsync(url);
            if (department == null)
            {
                return null;
            }

            var storeName = store?.Name;
            var departmentName = department?.Name;
            var departmentCategory = department?.Category;

            var httpClient = new HttpClient { Timeout = ScrappingConfiguration.HttpClientTimeout };
            var requestIdParam = "requestId=" + Guid.NewGuid();
            var requestUri = url.Contains('?') ? url + $"&{requestIdParam}" : url + $"?{requestIdParam}";
            string content = null;
            try
            {
                content = await httpClient.GetStringAsync(requestUri);
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
                    }

                    var name = productNameElement?.TextContent.Trim();

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
                                          IsAvailable = isAvailable
                                      };

                    product.Sha256 = JsonSerializer.Serialize(product).ComputeSHA256();

                    return product;
                }
            }

            return null;
        }
    }
}