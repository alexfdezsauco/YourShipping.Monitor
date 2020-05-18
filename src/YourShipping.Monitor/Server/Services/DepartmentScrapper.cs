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

    public class DepartmentScrapper : IEntityScrapper<Department>
    {
        private static readonly Regex[] ProductPatterns =
            {
                new Regex(@"<a\s+href=""Item[?]ProdPid=[^""]+""[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        private readonly Regex[] namePatterns =
            {
                new Regex(
                    "<a id=\".+_lnkDepartments\"[^>]+>([^<]+)</a>[^<]+</span>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        public async Task<Department> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestIdParam = "requestId=" + Guid.NewGuid();
            var requestUri = uri + $"&{requestIdParam}";
            var content = await httpClient.GetStringAsync(requestUri);

            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(content));
            var mainPanelElement = document.QuerySelector<IElement>("div#mainPanel");
            if (mainPanelElement != null)
            {
                content = mainPanelElement.OuterHtml.Replace(requestIdParam, "");
                var sha256 = content.ComputeSHA256();

                // TODO: Replace the usage of regex in favor of element selector
                var matches = ProductPatterns.Select(regex => regex.Matches(content))
                    .SelectMany(collection => collection).Where(match => match.Success).ToList();
                var departmentNameMatch = this.namePatterns.Select(regex => regex.Match(content))
                    .FirstOrDefault(match => match.Success);
                if (departmentNameMatch != null)
                {
                    var department = new Department
                                         {
                                             Url = uri,
                                             Name = departmentNameMatch?.Groups[1].Value,
                                             ProductsCount = matches.Count,
                                             Store = uri.Split('/')[3],
                                             Sha256 = sha256
                                         };

                    return department;
                }
            }

            return null;
        }
    }
}