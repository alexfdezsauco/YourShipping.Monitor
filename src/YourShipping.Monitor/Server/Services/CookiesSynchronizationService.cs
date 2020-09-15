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
    public class CookiesSynchronizationService : ICookiesSynchronizationService
    {
        private readonly Regex RegexA = new Regex(@"a\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexB = new Regex(@"b\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexC = new Regex(@"c\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexCall = new Regex(
            @"document\.cookie\s+=\s+""([^=]+)=""\s+[+]\s+toHex\(slowAES\.decrypt\(([^)]+)\)\)",
            RegexOptions.Compiled);

        private readonly Regex RegexCookiesTxt = new Regex(
            @"([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private CookieCollection CookieCollection;

        public CookiesSynchronizationService()
        {
            var fileSystemWatcher = new FileSystemWatcher("data", "cookies.txt")
                                        {
                                            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite
                                        };


            fileSystemWatcher.Changed += (sender, args) =>
                {
                    Log.Information("Cookies file changed");

                    this.InvalidateCookies();
                };

            fileSystemWatcher.Created += (sender, args) =>
                {
                    Log.Information("Cookies file created");

                    this.InvalidateCookies();
                };

            fileSystemWatcher.EnableRaisingEvents = true;
        }

        public DateTime SetDateTime { get; private set; }

        public CookieCollection GetCollection()
        {
            return this.GetCollectionAsync().GetAwaiter().GetResult();
        }

        public async Task<CookieCollection> GetCollectionAsync()
        {
            CookieCollection cookieCollection;

            await this.SemaphoreSlim.WaitAsync();

            if (this.CookieCollection != null)
            {
                cookieCollection = this.CookieCollection;
            }
            else
            {
                cookieCollection = this.LoadFromCookiesTxt();
                var antiScrappingCookie = await this.ReadAntiScrappingCookie();
                var cookie = cookieCollection.FirstOrDefault(c => c.Name == antiScrappingCookie?.Name);
                if (cookie != null)
                {
                    cookieCollection.Remove(cookie);
                }

                cookieCollection.Add(antiScrappingCookie);

                this.CookieCollection = cookieCollection;
                this.SetDateTime = DateTime.Now;
            }

            this.SemaphoreSlim.Release();

            return cookieCollection;
        }

        public void InvalidateCookies()
        {
            this.SemaphoreSlim.Wait();

            Log.Information("Invalidating Cookies...");
            this.CookieCollection = null;

            this.SemaphoreSlim.Release();
        }

        public void SyncCookies(CookieContainer cookieContainer)
        {
            this.SemaphoreSlim.Wait();

            if (this.CookieCollection != null)
            {
                var timeSpan = DateTime.Now.Subtract(this.SetDateTime);
                if (timeSpan.TotalSeconds > 30)
                {
                    Log.Information("Sync Cookies...");
                    this.CookieCollection = cookieContainer.GetCookies(new Uri("https://www.tuenvio.cu"));

                    this.SetDateTime = DateTime.Now;
                }
            }

            this.SemaphoreSlim.Release();
        }

        private CookieCollection LoadFromCookiesTxt()
        {
            var cookieCollection = new CookieCollection();
            var cookiesFile = "data/cookies.txt";
            if (File.Exists(cookiesFile))
            {
                var readAllText = File.ReadAllLines(cookiesFile).Where(s => !s.TrimStart().StartsWith("#"));
                foreach (var line in readAllText)
                {
                    var match = this.RegexCookiesTxt.Match(line);
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

                            cookieCollection.Add(new Cookie(name, value, match.Groups[3].Value, match.Groups[1].Value));
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e.Message);
                        }
                    }
                }
            }

            return cookieCollection;
        }

        private async Task<Cookie> ReadAntiScrappingCookie()
        {
            Cookie antiScrappingCookie = null;
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
                    var parametersMatch = this.RegexCall.Match(content);
                    if (parametersMatch.Success)
                    {
                        var cookieName = parametersMatch.Groups[1].Value.Trim();

                        var toNumbersACall = this.RegexA.Match(content).Groups[1].Value;
                        var toNumbersBCall = this.RegexB.Match(content).Groups[1].Value;
                        var toNumbersCCall = this.RegexC.Match(content).Groups[1].Value;

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

                        antiScrappingCookie = new Cookie(cookieName, cookieValue, "/", "www.tuenvio.cu");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error evaluating the Anti-Scrapping cookie.");
            }

            return antiScrappingCookie;
        }
    }
}