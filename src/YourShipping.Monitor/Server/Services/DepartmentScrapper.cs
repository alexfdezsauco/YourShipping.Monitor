namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class DepartmentScrapper : IEntityScrapper<Department>
    {
        private const string StorePrefix = "TuEnvio ";

        private readonly IBrowsingContext browsingContext;

        public DepartmentScrapper(IBrowsingContext browsingContext)
        {
            this.browsingContext = browsingContext;
        }

        public async Task<Department> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestIdParam = "requestId=" + Guid.NewGuid();
            var requestUri = uri + $"&{requestIdParam}";

            string content = null;
            try
            {
                content = await httpClient.GetStringAsync(requestUri);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error requesting Department '{url}'", uri);
            }

            if (!string.IsNullOrEmpty(content))
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

                var filterElement = mainPanelElement?.QuerySelector<IElement>("div.productFilter.clearfix");
                filterElement?.Remove();

                if (mainPanelElement != null)
                {
                    content = mainPanelElement.OuterHtml.Replace(requestIdParam, string.Empty);
                    var sha256 = content.ComputeSHA256();

                    var productElements = mainPanelElement.QuerySelectorAll<IElement>("li.span3.clearfix").ToList();
                    var departmentElement = mainPanelElement.QuerySelectorAll<IElement>("#mainPanel > span > a")
                        .LastOrDefault();
                    var departmentName = departmentElement?.InnerHtml.Trim();

                    if (!string.IsNullOrWhiteSpace(departmentName))
                    {
                        var department = new Department
                                             {
                                                 Url = uri,
                                                 Name = departmentName,
                                                 ProductsCount = productElements.Count,
                                                 Store = store,
                                                 Sha256 = sha256
                                             };

                        return department;
                    }
                }
            }

            return null;
        }
    }
}