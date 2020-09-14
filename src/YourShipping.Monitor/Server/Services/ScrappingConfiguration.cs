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

        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(10);

        private static readonly Random Random = new Random();

        public static TimeSpan DepartmentCacheExpiration => TimeSpan.FromSeconds(10);

        public static TimeSpan ProductCacheExpiration => TimeSpan.FromSeconds(10);

        public static TimeSpan StoreCacheExpiration => TimeSpan.FromMinutes(30);

        public static string RandomAgent()
        {
            return Agents[Random.Next(0, Agents.Length)];
        }
    }
}