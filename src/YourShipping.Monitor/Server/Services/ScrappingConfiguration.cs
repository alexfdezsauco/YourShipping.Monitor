namespace YourShipping.Monitor.Server.Services
{
    using System;

    internal class ScrappingConfiguration
    {
        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(10);

        public static TimeSpan DepartmentCacheExpiration => TimeSpan.FromSeconds(10);

        public static TimeSpan ProductCacheExpiration => TimeSpan.FromSeconds(10);

        public static TimeSpan StoreCacheExpiration => TimeSpan.FromMinutes(30);
    }
}