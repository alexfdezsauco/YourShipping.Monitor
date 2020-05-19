namespace YourShipping.Monitor.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    using AngleSharp;
    using AngleSharp.Dom;

    using Serilog;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    /// <summary>
    ///     The inspect department product scrapper.
    /// </summary>
    public class InspectDepartmentProductScrapper : IMultipleEntityScrapper<Product>
    {
        private readonly IBrowsingContext browsingContext;

        private readonly IEntityScrapper<Product> productScrapper;

        public InspectDepartmentProductScrapper(
            IBrowsingContext browsingContext,
            IEntityScrapper<Product> productScrapper)
        {
            this.browsingContext = browsingContext;
            this.productScrapper = productScrapper;
        }

        public async IAsyncEnumerable<Product> GetAsync(string url)
        {
            var products = new HashSet<string>();
            var httpClient = new HttpClient();
            bool found;
            var page = -1;
            do
            {
                page++;
                found = false;
                var requestIdParam = "requestId=" + Guid.NewGuid();
                var requestUri = url.Replace("&page=0", string.Empty, StringComparison.CurrentCultureIgnoreCase)
                                 + $"&page={page}&{requestIdParam}";
                string content = null;
                try
                {
                    content = await httpClient.GetStringAsync(requestUri);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error requesting Department '{url}'", requestUri);
                }

                if (!string.IsNullOrEmpty(content))
                {
                    var document = await this.browsingContext.OpenAsync(req => req.Content(content));
                    var elements = document.QuerySelectorAll<IElement>("li.span3.clearfix").ToList();

                    foreach (var element in elements)
                    {
                        var querySelector = element.QuerySelector<IElement>("a");
                        var querySelectorAttribute = querySelector.Attributes["href"];
                        var uri = new Uri(url);
                        var storeSegment = uri.Segments[1];
                        var pathAndQuery = querySelectorAttribute.Value;
                        var idx = url.IndexOf(uri.PathAndQuery, StringComparison.Ordinal);
                        var productUrl = url.Substring(0, idx + 1) + storeSegment + pathAndQuery;

                        if (products.Contains(productUrl))
                        {
                            break;
                        }

                        products.Add(productUrl);

                        found = true;

                        yield return await this.productScrapper.GetAsync(productUrl);
                    }
                }
            }
            while (found);
        }
    }
}