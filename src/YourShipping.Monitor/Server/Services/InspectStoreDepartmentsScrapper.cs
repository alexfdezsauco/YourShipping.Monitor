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
    using YourShipping.Monitor.Server.Services;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class InspectStoreDepartmentsScrapper : IMultiEntityScrapper<Department>
    {
        private readonly IBrowsingContext browsingContext;

        private readonly IEntityScrapper<Department> entityScrapper;

        public InspectStoreDepartmentsScrapper(
            IBrowsingContext browsingContext,
            IEntityScrapper<Department> entityScrapper)
        {
            this.browsingContext = browsingContext;
            this.entityScrapper = entityScrapper;
        }

        public async IAsyncEnumerable<Department> GetAsync(string url)
        {
            Log.Information("Scrapping Departments from {Url}", url);

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
                Log.Error(e, "Error requesting Store '{url}'", url);
            }

            if (!string.IsNullOrEmpty(content))
            {
                var document = await this.browsingContext.OpenAsync(req => req.Content(content));
                var mainNavElement = document.QuerySelector<IElement>("#mainContainer > header > div.mainNav");

                var elements = mainNavElement.QuerySelectorAll<IElement>("div > div > ul > li").ToList();
                foreach (var element in elements)
                {
                    if (!element.InnerHtml.Contains("<i class=\"icon-home\"></i>"))
                    {
                        var anchors = element.QuerySelectorAll<IElement>("div > ul > li > a");
                        foreach (var anchor in anchors)
                        {
                            var uri = new Uri(url);
                            var storeSegment = uri.Segments[1].Trim('/') + '/';
                            var pathAndQuery = anchor.Attributes["href"].Value;
                            if (pathAndQuery != "#")
                            {
                                var idx = url.IndexOf(uri.PathAndQuery, StringComparison.Ordinal);
                                var departmentUrl = url.Substring(0, idx + 1) + storeSegment + pathAndQuery;
                                var department = await this.entityScrapper.GetAsync(departmentUrl);
                                if (department != null)
                                {
                                    yield return department;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}