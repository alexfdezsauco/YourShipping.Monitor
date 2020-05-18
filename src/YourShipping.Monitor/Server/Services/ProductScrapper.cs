namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    /// <summary>
    ///     The product reader.
    /// </summary>
    public class ProductScrapper : IEntityScrapper<Product>
    {
        private readonly IBrowsingContext browsingContext;

        private readonly Regex[] namePatterns =
            {
                new Regex(
                    @"<td\s+class=""DescriptionValue""[^>]*>[^<]+<span>([^<]+)</span>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(
                    @"<div\s+class=""product-title"">\s+<h4>([^<]+)</h4>\s+</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        private readonly Regex[] pricePatterns =
            {
                new Regex(
                    @"<td\s+class=""PrecioProdList"">([^<]+)</td>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(
                    @"<div\s+class=""product-price"">\s+<span>([^<]+)</span>\s+</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        public ProductScrapper(IBrowsingContext browsingContext)
        {
            this.browsingContext = browsingContext;
        }

        public async Task<Product> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestIdParam = "requestId=" + Guid.NewGuid();
            var requestUri = uri + $"&{requestIdParam}";
            var content = await httpClient.GetStringAsync(requestUri);

            var document = await this.browsingContext.OpenAsync(req => req.Content(content));
            var mainPanelElement = document.QuerySelector<IElement>("div#mainPanel");
            if (mainPanelElement != null)
            {
                content = mainPanelElement.OuterHtml.Replace(requestIdParam, string.Empty);
                var sha256 = content.ComputeSHA256();

                // TODO: Replace the usage of regex in favor of element selector
                var nameMatch = this.namePatterns.Select(regex => regex.Match(content)).FirstOrDefault(m => m.Success);
                var priceMatch = this.pricePatterns.Select(regex => regex.Match(content))
                    .FirstOrDefault(m => m.Success);
                var name = nameMatch?.Groups[1].Value;
                var priceText = priceMatch?.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(priceText) && !string.IsNullOrEmpty(name))
                {
                    var priceParts = priceText?.Split(' ');
                    var price = float.Parse(priceParts[0].Trim(' ', '$'));
                    var currency = priceParts[^1];

                    return new Product
                               {
                                   Name = name,
                                   Price = price,
                                   Currency = currency,
                                   Url = uri,
                                   Store = uri.Split('/')[3],
                                   IsAvailable = !content.Contains("no esta disponible"),
                                   Sha256 = sha256
                               };
                }
            }

            return null;
        }
    }
}