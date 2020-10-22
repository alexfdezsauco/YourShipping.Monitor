namespace YourShipping.Monitor.Server.Services
{
    using System;

    internal class ScraperConfigurations
    {
        public const string RootAddress = "https://www.tuenvio.cu/";

        public static readonly Uri CookieCollectionUrl = new Uri("https://www.tuenvio.cu");

        public static readonly TimeSpan DepartmentCacheExpiration = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(20);

        public static readonly TimeSpan ProductCacheExpiration = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan StoreCacheExpiration = TimeSpan.FromMinutes(30);

        public static readonly string StoresJsonUrl = "https://www.tuenvio.cu/stores.json";

        public static readonly string[] SupportedAgents =
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.75 Safari/537.36"
                // "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:80.0) Gecko/20100101 Firefox/80.0"
            };

        private static readonly int UserAgentIndex = new Random().Next(0, SupportedAgents.Length);

        public static string GetSupportedAgent()
        {
            return SupportedAgents[UserAgentIndex];
        }
    }
}