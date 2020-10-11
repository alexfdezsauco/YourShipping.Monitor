namespace YourShipping.Monitor.Server.Helpers
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class UriHelper
    {
        private static readonly Regex UrlSlugPattern = new Regex("([^/]+)", RegexOptions.Compiled);

        public static string EnsureDepartmentUrl(string url)
        {
            return Regex.Replace(
                url,
                @"(&?)(ProdPid=\d+(&?)|page=\d+(&?)|img=\d+(&?))",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ').Replace("/Item", "/Products");
        }

        public static string EnsureProductUrl(string url)
        {
            return Regex.Replace(url, @"(&?)(page=\d+(&?)|img=\d+(&?))", string.Empty, RegexOptions.IgnoreCase)
                .Trim(' ');
        }

        public static string EnsureStoreUrl(string url)
        {
            var uri = new Uri(url);
            return
                $"{uri.Scheme}://{uri.DnsSafeHost}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : string.Empty)}/{uri.Segments[1].Trim(' ', '/')}/Products?depPid=0";
        }

        public static string EscapeLargeDataString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            const int limit = 32766;
            var count = value.Length / limit;

            var sb = new StringBuilder();

            for (var i = 0; i < count; i++)
            {
                sb.Append(Uri.EscapeDataString(value.Substring(limit * i, limit)));
            }

            sb.Append(Uri.EscapeDataString(value.Substring(limit * count)));

            return sb.ToString();
        }

        public static string GetStoreSlug(string uri)
        {
            var matchCollection = UrlSlugPattern.Matches(uri);
            if (matchCollection.Count > 3)
            {
                var match = matchCollection[2];
                return match.Value;
            }

            return "/";
        }
    }
}