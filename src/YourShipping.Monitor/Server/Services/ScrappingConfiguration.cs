namespace YourShipping.Monitor.Server.Services
{
    using System;

    internal class ScrappingConfiguration
    {
        public static readonly string[] Agents =
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.83 Safari/537.36 Edg/85.0.564.44",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:80.0) Gecko/20100101 Firefox/80.0"
            };

        public static readonly Uri CookieCollectionUrl = new Uri("https://www.tuenvio.cu");

        public static readonly TimeSpan DepartmentCacheExpiration = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(20);

        public static readonly TimeSpan ProductCacheExpiration = TimeSpan.FromSeconds(10);

        public static readonly TimeSpan StoreCacheExpiration = TimeSpan.FromMinutes(30);

        public static readonly string StoresJsonUrl = "https://www.tuenvio.cu/stores.json";

        private static readonly int AgentIndex = new Random().Next(0, Agents.Length);

        private static readonly Random Random = new Random();

        public static string GetAgent()
        {
            return Agents[AgentIndex];
        }
    }
}