namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

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

        public ProductScrapper(IBrowsingContext browsingContext)
        {
            this.browsingContext = browsingContext;
        }

        public async Task<Product> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestIdParam = "requestId=" + Guid.NewGuid();
            var requestUri = uri.Contains('?') ? uri + $"&{requestIdParam}" : uri + $"?{requestIdParam}";
            string content = null;
            try
            {
                content = await httpClient.GetStringAsync(requestUri);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting Department '{url}'", uri);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await this.browsingContext.OpenAsync(req => req.Content(content));

                var footerElement = document.QuerySelector<IElement>("#footer > div.container > div > div > p");

                string store = null;
                var uriParts = uri.Split('/');
                if (uriParts.Length > 3)
                {
                    store = uri.Split('/')[3];
                }

                var footerElementTextParts = footerElement.InnerHtml.Split('•');
                if (footerElementTextParts.Length > 0)
                {
                    store = footerElementTextParts[^1].Trim();
                    if (store.StartsWith(StorePrefix, StringComparison.CurrentCultureIgnoreCase)
                        && store.Length > StorePrefix.Length)
                    {
                        store = store.Substring(StorePrefix.Length - 1);
                    }
                }

                var mainPanelElement = document.QuerySelector<IElement>("div#mainPanel");
                if (mainPanelElement != null)
                {
                    content = mainPanelElement.OuterHtml.Replace(requestIdParam, string.Empty);
                    var sha256 = content.ComputeSHA256();

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

                    var name = productNameElement?.InnerHtml.Trim();

                    float price = 0;
                    string currency = null;
                    var priceParts = productPriceElement?.InnerHtml?.Trim().Split(' ');
                    if (priceParts != null && priceParts.Length > 0)
                    {
                        price = float.Parse(priceParts[0].Trim(' ', '$'));
                        currency = priceParts[^1];
                    }

                    return new Product
                               {
                                   Name = name,
                                   Price = price,
                                   Currency = currency,
                                   Url = uri,
                                   Store = store,
                                   IsAvailable = isAvailable,
                                   Sha256 = sha256
                               };
                }
            }

            return null;
        }
    }
}