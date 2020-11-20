namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.IO;

    using Serilog;

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
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36"
            };

        private static int UserAgentIndex
        {
            get { return new Random().Next(0, SupportedAgents.Length); }
        }

        public static string GetSupportedAgent()
        {
            string[] agents = null;
            try
            {
                var path = "data/user-agent.txt";
                if (File.Exists(path))
                {
                    agents = File.ReadAllLines(path);
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error reading user-agent.txt file");
            }

            return agents == null ||  agents.Length == 0 ? SupportedAgents[UserAgentIndex] : agents[new Random().Next(0, agents.Length)];
        }
    }
}