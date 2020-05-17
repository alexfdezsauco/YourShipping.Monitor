namespace YourShipping.WishList.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    /// <summary>
    ///     The product reader.
    /// </summary>
    public class ProductScrapper : IEntityScrapper<Product>
    {
        private static readonly Regex[] NamePatterns =
            {
                new Regex(
                    @"<td\s+class=""DescriptionValue""[^>]*>[^<]+<span>([^<]+)</span>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(
                    @"<div\s+class=""product-title"">\s+<h4>([^<]+)</h4>\s+</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        private static readonly Regex[] PricePatterns =
            {
                new Regex(
                    @"<td\s+class=""PrecioProdList"">([^<]+)</td>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(
                    @"<div\s+class=""product-price"">\s+<span>([^<]+)</span>\s+</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        public async Task<Product> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestUri = uri + "&requestId=" + Guid.NewGuid().ToString();
            var content = await httpClient.GetStringAsync(requestUri);
            var nameMatch = NamePatterns.Select(regex => regex.Match(content)).FirstOrDefault(m => m.Success);
            var priceMatch = PricePatterns.Select(regex => regex.Match(content)).FirstOrDefault(m => m.Success);
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
                    IsAvailable = !content.Contains("no esta disponible")
                };
            }

            return null;
        }
    }
}