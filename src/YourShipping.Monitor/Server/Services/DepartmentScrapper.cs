namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using Catel.Caching;
    using Catel.Caching.Policies;

    using Microsoft.Extensions.DependencyInjection;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class DepartmentScrapper : IEntityScrapper<Department>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        private readonly ICacheStorage<string, Department> cacheStorage;

        private readonly HttpClient httpClient;

        private readonly IServiceProvider serviceProvider;

        private readonly IEntityScrapper<Store> storeScrapper;

        public DepartmentScrapper(IBrowsingContext browsingContext,
                                  IEntityScrapper<Store> storeScrapper,
                                  ICacheStorage<string, Department> cacheStorage,
                                  HttpClient httpClient,
                                  IServiceProvider serviceProvider)
        {
            this.browsingContext = browsingContext;
            this.storeScrapper = storeScrapper;
            this.cacheStorage = cacheStorage;
            this.httpClient = httpClient;
            this.serviceProvider = serviceProvider;
        }

        public async Task<Department> GetAsync(string url, bool force = false, params object[] parents)
        {
            var store = parents?.OfType<Store>().FirstOrDefault();
            url = Regex.Replace(
                url,
                @"(&?)(ProdPid=\d+(&?)|page=\d+(&?)|img=\d+(&?))",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ').Replace("/Item", "/Products");

            return await this.cacheStorage.GetFromCacheOrFetchAsync(
                       $"{url}/{store != null}",
                       async () => await this.GetDirectAsync(url, store),
                       ExpirationPolicy.Duration(ScrappingConfiguration.Expiration),
                       force);
        }

        private async Task<Department> GetDirectAsync(string url, Store parentStore)
        {
            var productScrapper = this.serviceProvider.GetService<IEntityScrapper<Product>>();

            Log.Information("Scrapping Department from {Url}", url);

            var store = parentStore ?? await this.storeScrapper.GetAsync(url);
            if (store == null)
            {
                return null;
            }

            var storeName = store?.Name;
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

            if (!string.IsNullOrEmpty(content))
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
                    var filterElement = mainPanelElement?.QuerySelector<IElement>("div.productFilter.clearfix");
                    filterElement?.Remove();



                    var departmentElements = mainPanelElement.QuerySelectorAll<IElement>("#mainPanel > span > a")
                        .ToList();

                    Department department = null;
                    if (departmentElements.Count > 2)
                    {
                        var departmentCategory = departmentElements[^2].TextContent.Trim();
                        var departmentName = departmentElements[^1].TextContent.Trim();

                        if (!string.IsNullOrWhiteSpace(departmentName) && !string.IsNullOrWhiteSpace(departmentCategory))
                        {
                            department = new Department
                                                        {
                                                            Url = url,
                                                            Name = departmentName,
                                                            Category = departmentCategory,
                                                            Store = storeName,
                                                            IsAvailable = true
                                                        };

                        }
                    }

                    var productElements = mainPanelElement.QuerySelectorAll<IElement>("li.span3.clearfix").ToList();
                    var productsCount = 0;
                    var baseUrl = Regex.Replace(url, "/(Products|Item)[?]depPid=\\d+", string.Empty, RegexOptions.Singleline);
                    foreach (var productElement in productElements)
                    {
                        var element = productElement.QuerySelector<IElement>("a");
                        var elementAttribute = element.Attributes["href"];
                        var product = await productScrapper.GetAsync($"{baseUrl}/{elementAttribute.Value}", force:false, store, department);
                        if (product != null && product.IsAvailable)
                        {
                            productsCount++;
                        }
                    }

                    if (department != null)
                    {
                        department.ProductsCount = productsCount;
                        department.Sha256 = JsonSerializer.Serialize(department).ComputeSHA256();
                    }

                    return department;
                }
            }

            return null;
        }
    }
}