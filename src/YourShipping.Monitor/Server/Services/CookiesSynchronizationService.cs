namespace YourShipping.Monitor.Server.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Network;
    using AngleSharp.Js;

    using Catel.Caching;
    using Catel.Collections;

    using Microsoft.Extensions.DependencyInjection;

    using Newtonsoft.Json;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Services;

    // TODO: Improve this?
    public class CookiesSynchronizationService : ICookiesSynchronizationService
    {
        private readonly CacheStorage<string, Dictionary<string, Cookie>> cookieCollectionCacheStorage =
            new CacheStorage<string, Dictionary<string, Cookie>>();

        private readonly FileSystemWatcher fileSystemWatcher;

        private readonly IServiceProvider provider;

        private readonly Regex RegexA = new Regex(@"a\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexB = new Regex(@"b\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexC = new Regex(@"c\s*=\s*(toNumbers[^)]+\))", RegexOptions.Compiled);

        private readonly Regex RegexCall = new Regex(
            @"document\.cookie\s+=\s+""([^=]+)=""\s+[+]\s+toHex\(slowAES\.decrypt\(([^)]+)\)\)",
            RegexOptions.Compiled);

        private readonly Regex RegexCookiesTxt = new Regex(
            @"([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public CookiesSynchronizationService(IServiceProvider provider)
        {
            this.provider = provider;
            this.fileSystemWatcher = new FileSystemWatcher("data", "cookies.txt")
                                         {
                                             NotifyFilter =
                                                 NotifyFilters.LastWrite | NotifyFilters.CreationTime
                                                                         | NotifyFilters.FileName
                                         };

            this.fileSystemWatcher.Changed += (sender, args) =>
                {
                    Log.Information("Cookies file changed");

                    this.cookieCollectionCacheStorage.Clear();
                };

            this.fileSystemWatcher.Created += (sender, args) =>
                {
                    Log.Information("Cookies file created");

                    this.cookieCollectionCacheStorage.Clear();
                };

            this.fileSystemWatcher.EnableRaisingEvents = true;
        }

        public async Task<HttpClient> CreateHttpClientAsync(string url)
        {
            var httpClient = this.provider.GetService<HttpClient>();

            var clientHandler = httpClient.GetHttpClientHandler();
            var cookieCollection = await this.GetCookieCollectionAsync(url);

            clientHandler.CookieContainer.Add(ScrappingConfiguration.CookieCollectionUrl, cookieCollection);

            return httpClient;
        }

        public async Task<CookieCollection> GetCookieCollectionAsync(string url)
        {
            var cookieCollection = new CookieCollection();

            var storedCookieCollection = await this.GetCookiesCollectionFromCacheAsync(url);
            lock (storedCookieCollection)
            {
                cookieCollection.AddRange(storedCookieCollection.Values);
            }

            return cookieCollection;
        }

        public async Task<Dictionary<string, Cookie>> GetCookiesCollectionAsync()
        {
            var cookieCollection = await this.LoadFromCookiesTxt();
            var antiScrappingCookie = await this.ReadAntiScrappingCookie();
            if (antiScrappingCookie != null)
            {
                cookieCollection.Remove(antiScrappingCookie.Name);
                cookieCollection.Add(antiScrappingCookie.Name, antiScrappingCookie);
            }

            return cookieCollection;
        }

        public void InvalidateCookies(string url)
        {
            Log.Information("Invalidating Cookies for url '{Url}'...", url);

            var parts = new Url(url).Path.Split('/');
            try
            {
                if (parts.Length > 1)
                {
                    var cookieFilePath = $"data/{parts[0]}.json";
                    Log.Information("Deleting cookies file for {Path}.", cookieFilePath);
                    File.Delete(cookieFilePath);
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error deleting cookies file for {Url}", url);
            }

            this.cookieCollectionCacheStorage.Remove(url);
        }

        public async Task SerializeAsync()
        {
            Log.Information("Serializing cookies...");
            var urls = this.cookieCollectionCacheStorage.Keys.ToList();
            foreach (var url in urls)
            {
                var storedCookieCollection = await this.GetCookiesCollectionFromCacheAsync(url);
                lock (storedCookieCollection)
                {
                    try
                    {
                        var parts = new Url(url).Path.Split('/');
                        if (parts.Length > 1)
                        {
                            var cookiesFilePath = $"data/{parts[0]}.json";
                            Log.Information("Serializing cookies for {Path}.", cookiesFilePath);
                            var serializeObject = JsonConvert.SerializeObject(storedCookieCollection);
                            File.WriteAllText(cookiesFilePath, serializeObject, Encoding.UTF8);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error serializing cookies.");
                    }
                }
            }
        }

        public async Task SyncCookiesAsync(HttpClient httpClient, string url)
        {
            var httpClientHandler = httpClient.GetHttpClientHandler();
            var cookieContainer = httpClientHandler.CookieContainer;
            var cookieCollection = cookieContainer.GetCookies(ScrappingConfiguration.CookieCollectionUrl);
            if (cookieCollection[".ASPXANONYMOUS"] != null)
            {
                Log.Warning("Session expires. Cookies will be invalidated.");
                this.InvalidateCookies(url);
            }
            else
            {
                await this.SyncCookiesAsync(url, cookieCollection);
            }
        }

        public async Task SyncCookiesAsync(string url, CookieCollection cookieCollection)
        {
            var storedCookieCollection = await this.GetCookiesCollectionFromCacheAsync(url);
            lock (storedCookieCollection)
            {
                Log.Information("Synchronizing cookies for url '{Url}'...", url);

                foreach (Cookie cookie in cookieCollection)
                {
                    if (!storedCookieCollection.TryGetValue(cookie.Name, out var storedCookie))
                    {
                        Log.Information(
                            "Adding cookie '{CookieName}' with value '{CookieValue}' for url '{Url}'.",
                            cookie.Name,
                            cookie.Value,
                            url);

                        storedCookieCollection.Add(cookie.Name, cookie);
                    }
                    else if (storedCookie.Value != cookie.Value)
                    {
                        Log.Information(
                            "Synchronizing cookie '{CookieName}' with value '{CookieValue}' for url '{Url}'.",
                            cookie.Name,
                            cookie.Value,
                            url);

                        storedCookie.Value = cookie.Value;
                    }
                }
            }
        }

        private async Task<Dictionary<string, Cookie>> GetCookiesCollectionFromCacheAsync(string url)
        {
            return await this.cookieCollectionCacheStorage.GetFromCacheOrFetchAsync(
                       url,
                       async () =>
                           {
                               var parts = new Url(url).Path.Split('/');
                               try
                               {
                                   if (parts.Length > 1)
                                   {
                                       var cookieFilePath = $"data/{parts[0]}.json";
                                       if (File.Exists(cookieFilePath))
                                       {
                                           Log.Information("Deserializing cookies from {Path}.", cookieFilePath);
                                           var readAllText = File.ReadAllText(cookieFilePath, Encoding.UTF8);
                                           return JsonConvert
                                               .DeserializeObject<Dictionary<string, Cookie>>(readAllText);
                                       }
                                   }
                               }
                               catch (Exception e)
                               {
                                   Log.Warning(e, "Error deserializing cookies");
                               }

                               return await this.GetCookiesCollectionAsync();
                           });
        }

        private async Task<Dictionary<string, Cookie>> LoadFromCookiesTxt()
        {
            var cookieCollection = new Dictionary<string, Cookie>();
            var cookiesFile = "data/cookies.txt";
            if (File.Exists(cookiesFile))
            {
                var readAllText =
                    (await File.ReadAllLinesAsync(cookiesFile)).Where(s => !s.TrimStart().StartsWith("#"));
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

                            if (name != "SRVNAME")
                            {
                                cookieCollection.Add(
                                    name,
                                    new Cookie(name, value, match.Groups[3].Value, match.Groups[1].Value));
                            }
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
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "user-agent",
                    ScrappingConfiguration.GetAgent());

                var requester = new HttpClientRequester(httpClient);
                var config = Configuration.Default.WithRequester(requester)
                    .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }).WithJs();

                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(ScrappingConfiguration.StoresJsonUrl).WaitUntilAvailable();

                var content = document.Body.TextContent;
                var match = Regex.Match(content, @"Server\sError\s+406");
                if (!match.Success && !string.IsNullOrWhiteSpace(content))
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

                        Log.Information(
                            "Read cookie '{CookieName}' with value '{CookieValue}'",
                            cookieName,
                            cookieValue);

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