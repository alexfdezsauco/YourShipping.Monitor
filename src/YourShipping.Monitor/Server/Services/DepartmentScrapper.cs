namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    public class DepartmentScrapper : IEntityScrapper<Department>
    {
        private static readonly Regex[] NamePatterns =
            {
                new Regex(
                    "<a id=\".+_lnkDepartments\"[^>]+>([^<]+)</a>[^<]+</span>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        private static readonly Regex[] ProductPatterns =
            {
                new Regex(@"<a\s+href='Item[?]ProdPid=[^']+'[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        public async Task<Department> GetAsync(string uri)
        {
            var httpClient = new HttpClient();
            var requestUri = uri + "&requestId=" + Guid.NewGuid().ToString();
            var content = await httpClient.GetStringAsync(requestUri);
            var matches = ProductPatterns.Select(regex => regex.Matches(content)).SelectMany(collection => collection)
                .Where(match => match.Success).ToList();
            var departmentNameMatch = NamePatterns.Select(regex => regex.Match(content))
                .FirstOrDefault(match => match.Success);
            if (departmentNameMatch != null)
            {
                var department = new Department
                {
                    Url = uri,
                    Name = departmentNameMatch?.Groups[1].Value,
                    ProductsCount = matches.Count,
                    Store = uri.Split('/')[3],
                };

                return department;
            }

            return null;
        }
    }
}