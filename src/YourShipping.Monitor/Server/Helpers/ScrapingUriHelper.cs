using System;
using System.Text.RegularExpressions;

namespace YourShipping.Monitor.Server.Helpers
{
    public static class ScrapingUriHelper
    {
        private static readonly Regex UrlSlugPattern = new Regex("([^/]+)", RegexOptions.Compiled);

        private static string GetStoreSlug(string uri)
        {
            var matchCollection = UrlSlugPattern.Matches(uri);
            if (matchCollection.Count > 3)
            {
                var match = matchCollection[2];
                return match.Value;
            }

            return "/";
        }

        public static string EnsureProductUrl(string url)
        {
            return Regex.Replace(
                url,
                @"(&?)(page=\d+(&?)|img=\d+(&?))",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ');
        }

        public static string EnsureDepartmentUrl(string url)
        {
            return Regex.Replace(
                url,
                @"(&?)(ProdPid=\d+(&?)|page=\d+(&?)|img=\d+(&?))",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ').Replace("/Item", "/Products");
        }

        public static string EnsureStoreUrl(string url)
        {
            var uri = new Uri(url);
            return
                $"{uri.Scheme}://{uri.DnsSafeHost}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : string.Empty)}/{uri.Segments[1].Trim(' ', '/')}/Products?depPid=0";
        }
    }
}