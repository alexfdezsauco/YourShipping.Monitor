namespace YourShipping.Monitor.Server.Services
{
    using System;

    internal class ScrappingConfiguration
    {
        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(5);

        private static readonly Random Random = new Random();

        public static TimeSpan Expiration => TimeSpan.FromMinutes(Random.Next(1, 3) * Random.NextDouble());
    }
}