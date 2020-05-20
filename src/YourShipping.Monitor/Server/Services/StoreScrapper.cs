namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class StoreScrapper : IEntityScrapper<Store>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        public StoreScrapper(IBrowsingContext browsingContext)
        {
            this.browsingContext = browsingContext;
        }

        public async Task<Store> GetAsync(string uri)
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
                Log.Error(e, "Error requesting Store from '{url}'", uri);
            }

            var categoriesCount = 0;
            var departmentsCount = 0;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var document = await this.browsingContext.OpenAsync(req => req.Content(content));

                var footerElement = document.QuerySelector<IElement>("#footer > div.container > div > div > p");

                string storeName = null;
                var uriParts = uri.Split('/');
                if (uriParts.Length > 3)
                {
                    storeName = uri.Split('/')[3];
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

                var mainNavElement = document.QuerySelector<IElement>("#mainContainer > header > div.mainNav");

                if (mainNavElement != null)
                {
                    var sha256 = mainNavElement.OuterHtml.Replace(requestIdParam, string.Empty).ComputeSHA256();
                    var elements = mainNavElement.QuerySelectorAll<IElement>("div > div > ul > li").ToList();
                    foreach (var element in elements)
                    {
                        if (!element.InnerHtml.Contains("<i class=\"icon-home\"></i>"))
                        {
                            categoriesCount++;
                            var querySelector = element.QuerySelector<IElement>("a");
                            if (querySelector != null)
                            {
                                querySelector.QuerySelector("i")?.Remove();
                                var name = querySelector.TextContent;
                            }

                            var departmentsElementSelector =
                                element.QuerySelectorAll<IElement>("div > ul > li").ToList();
                            departmentsCount += departmentsElementSelector.Count;
                        }
                    }

                    var store = new Store
                                    {
                                        Name = storeName,
                                        DepartmentsCount = departmentsCount,
                                        CategoriesCount = categoriesCount,
                                        Url = uri,
                                        IsAvailable = true
                                    };

                    store.Sha256 = JsonSerializer.Serialize(store).ComputeSHA256();
                    return store;
                }
            }

            return null;
        }
    }
}