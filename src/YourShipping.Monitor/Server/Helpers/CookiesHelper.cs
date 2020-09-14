namespace YourShipping.Monitor.Server.Helpers
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Network;
    using AngleSharp.Js;

    using Serilog;

    using YourShipping.Monitor.Server.Services;

    // TODO: Improve this?
    internal static class CookiesHelper
    {
        private static readonly object _syncObj = new object();

        private static readonly Regex RegexA = new Regex(@"a\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private static readonly Regex RegexB = new Regex(@"b\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private static readonly Regex RegexC = new Regex(@"c\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private static readonly Regex RegexCall = new Regex(
            @"document\.cookie\s+=\s+""([^=]+)=""\s+[+]\s+toHex\(slowAES\.decrypt\(([^)]+)\)\)",
            RegexOptions.Compiled);

        private static readonly Regex RegexCookiesTxt = new Regex(
            @"([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private static Cookie AntiScrappingCookie;

        private static CookieCollection CookieCollection;

        // TODO: Review this method if something stop working.
        public static async Task<CookieCollection> GetCollectionAsync()
        {
            await SemaphoreSlim.WaitAsync();

            if (CookieCollection != null)
            {
                SemaphoreSlim.Release();

                return CookieCollection;
            }

            SemaphoreSlim.Release();

            var collection = new CookieCollection();
            var cookiesFile = "data/cookies.txt";
            if (File.Exists(cookiesFile))
            {
                var readAllText = File.ReadAllLines(cookiesFile).Where(s => !s.TrimStart().StartsWith("#"));
                foreach (var line in readAllText)
                {
                    var match = RegexCookiesTxt.Match(line);
                    if (match.Success)
                    {
                        try
                        {
                            var name = match.Groups[6].Value;
                            var value = match.Groups[7].Value;

                            if (name == "myCookie")
                            {
                                value = "username=&userPsw=";
                            }

                            collection.Add(new Cookie(name, value, match.Groups[3].Value, match.Groups[1].Value));
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e.Message);
                        }
                    }
                }
            }

            await SemaphoreSlim.WaitAsync();
            if (AntiScrappingCookie == null)
            {
                Log.Information("Initializing Anti-Scrapping Cookie...");
                try
                {
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        "user-agent",
                        ScrappingConfiguration.GetAgent());

                    var requester = new HttpClientRequester(httpClient);
                    var config = Configuration.Default.WithRequester(requester)
                        .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }).WithJs();

                    var context = BrowsingContext.New(config);
                    var document = await context.OpenAsync("https://www.tuenvio.cu/stores.json").WaitUntilAvailable();

                    var content = document.Body.TextContent;
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var parametersMatch = RegexCall.Match(content);
                        if (parametersMatch.Success)
                        {
                            var cookieName = parametersMatch.Groups[1].Value.Trim();

                            var toNumbersACall = RegexA.Match(content).Groups[1].Value;
                            var toNumbersBCall = RegexB.Match(content).Groups[1].Value;
                            var toNumbersCCall = RegexC.Match(content).Groups[1].Value;

                            var parameters = parametersMatch.Groups[2].Value;
                            parameters = parameters.Replace("a", "%A%").Replace("b", "%B%").Replace("c", "%C%");
                            parameters = parameters.Replace("%A%", toNumbersACall).Replace("%B%", toNumbersBCall)
                                .Replace("%C%", toNumbersCCall);

                            // Review: looks like the WaitUntilAvailable method is not working properly.
                            var cookieValue = string.Empty;
                            while (string.IsNullOrWhiteSpace(cookieValue))
                            {
                                try
                                {
                                    cookieValue = document.ExecuteScript($"toHex(slowAES.decrypt({parameters}))")
                                        .ToString();
                                }
                                catch (Exception e)
                                {
                                    Log.Warning(e, "Error retrieving the Anti-Scrapping cookie");

                                    await Task.Delay(100);
                                }
                            }

                            AntiScrappingCookie = new Cookie(cookieName, cookieValue, "/", "www.tuenvio.cu");
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error evaluating the Anti-Scrapping cookie.");
                }
            }

            var cookie = collection.FirstOrDefault(c => c.Name == AntiScrappingCookie?.Name);
            if (cookie != null)
            {
                collection.Remove(cookie);
            }

            collection.Add(AntiScrappingCookie);

            SemaphoreSlim.Release();

            return collection;
        }

        // TODO: Where this method should be called? 
        public static void InvalidateCookies()
        {
            Log.Information("Invalidating Cookies...");

            SemaphoreSlim.Wait();

            AntiScrappingCookie = null;
            CookieCollection = null;

            SemaphoreSlim.Release();
        }

        public static void SyncCookies(CookieContainer cookieContainer)
        {
            SemaphoreSlim.Wait();
            CookieCollection = cookieContainer.GetCookies(new Uri("https://www.tuenvio.cu"));
            SemaphoreSlim.Release();
        }
    }
}