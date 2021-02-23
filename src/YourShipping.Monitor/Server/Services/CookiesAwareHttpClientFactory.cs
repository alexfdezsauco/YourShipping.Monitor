namespace YourShipping.Monitor.Server.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

    using Catel;
    using Catel.Collections;

    using Microsoft.Extensions.DependencyInjection;

    using Newtonsoft.Json;

    using Serilog;

    using Tesseract;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Services;

    using Enumerable = System.Linq.Enumerable;
    using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

    // TODO: Improve this?
    public class CookiesAwareHttpClientFactory : ICookiesAwareHttpClientFactory
    {
        private readonly Dictionary<string, bool> _authenticating = new Dictionary<string, bool>();

        private readonly IConfiguration _configuration;

        private readonly Dictionary<string, Dictionary<string, Cookie>> _loginCookies =
            new Dictionary<string, Dictionary<string, Cookie>>();

        private readonly object _syncObj = new object();

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

        public CookiesAwareHttpClientFactory(IServiceProvider provider, IConfiguration configuration)
        {
            this.provider = provider;
            this._configuration = configuration;
        }

        public async Task BeginLoginAsync(string url)
        {
            var storeSlug = UriHelper.GetStoreSlug(url);
            lock (this._syncObj)
            {
                if (this._authenticating.TryGetValue(storeSlug, out var authenticating) && authenticating)
                {
                    return;
                }

                this._authenticating[storeSlug] = true;
            }

            var antiScrappingCookie = await this.ReadAntiScrappingCookieAsync();
            var credentialsConfigurationSection = this._configuration.GetSection("Credentials");
            var username = credentialsConfigurationSection?["Username"];
            var password = credentialsConfigurationSection?["Password"];
            bool.TryParse(credentialsConfigurationSection?["Unattended"], out var unattended);
            if (!string.IsNullOrWhiteSpace(username) && username != "%USERNAME%"
                                                     && !string.IsNullOrWhiteSpace(password))
            {
                var cookieCollection = await this.LoginAsync(antiScrappingCookie, url, username, password, unattended);
                if (cookieCollection != null)
                {
                    lock (this._syncObj)
                    {
                        this._loginCookies[storeSlug] = cookieCollection;
                    }
                }
            }
        }

        public async Task<HttpClient> CreateHttpClientAsync(string url)
        {
            HttpClient httpClient = null;

            if (url == ScraperConfigurations.StoresJsonUrl)
            {
                httpClient = this.provider.GetService<HttpClient>();
            }
            else
            {
                var cookieCollection = await this.GetCookieCollectionAsync(url);
                if (cookieCollection.Count > 0)
                {
                    httpClient = this.provider.GetService<HttpClient>();
                    var clientHandler = httpClient.GetHttpClientHandler();
                    clientHandler.CookieContainer.Add(ScraperConfigurations.CookieCollectionUrl, cookieCollection);
                }
            }

            return httpClient;
        }

        public void InvalidateCookies(string url)
        {
            // TODO: do also something with authentication cookies?
            Log.Information("Invalidating Cookies for url '{Url}'...", url);
            var storeSlug = UriHelper.GetStoreSlug(url);
            if (storeSlug != "/")
            {
                lock (this._syncObj)
                {
                    this._loginCookies.Remove(storeSlug);
                    this._authenticating.Remove(storeSlug);
                }

                try
                {
                    var cookieFilePath = $"data/{storeSlug}.json";
                    Log.Information("Deleting cookies file for {Path}.", cookieFilePath);
                    File.Delete(cookieFilePath);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error deleting cookies file for {Url}", url);
                }
            }
        }

        public Task SerializeAsync()
        {
            Log.Information("Serializing cookies...");
            lock (this._syncObj)
            {
                foreach (var pair in this._loginCookies)
                {
                    var storeSlug = pair.Key;
                    var storedCookieCollection = pair.Value;
                    if (storeSlug != "/")
                    {
                        try
                        {
                            var cookiesFilePath = $"data/{storeSlug}.json";
                            Log.Information("Serializing cookies for {Path}.", cookiesFilePath);
                            var serializeObject = JsonConvert.SerializeObject(
                                storedCookieCollection,
                                Formatting.Indented);
                            File.WriteAllText(cookiesFilePath, serializeObject, Encoding.UTF8);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, "Error serializing cookies.");
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        public async Task SyncCookiesAsync(string url, HttpClient httpClient)
        {
            var httpClientHandler = httpClient.GetHttpClientHandler();
            var cookieContainer = httpClientHandler.CookieContainer;
            var cookieCollection = cookieContainer.GetCookies(ScraperConfigurations.CookieCollectionUrl);
            if (!string.IsNullOrWhiteSpace(cookieCollection[".ASPXANONYMOUS"]?.Value))
            {
                Log.Warning("Session expires. Cookies will be invalidated.");

                this.InvalidateCookies(url);
            }
            else
            {
                await this.SyncCookiesAsync(url, cookieCollection);
            }
        }

        private static async Task<Dictionary<string, string>> BuildSignInParametersAsync(
            string username,
            string password,
            string signinPageContent)
        {
            Argument.IsNotNullOrWhitespace(() => username);
            Argument.IsNotNullOrWhitespace(() => password);
            Argument.IsNotNullOrWhitespace(() => signinPageContent);

            Dictionary<string, string> parameters = null;
            try
            {
                var browsingContext = BrowsingContext.New(Configuration.Default);
                var signinPageDocument = await browsingContext.OpenAsync(req => req.Content(signinPageContent));
                parameters = new Dictionary<string, string>
                                 {
                                     { "__EVENTTARGET", string.Empty },
                                     { "__EVENTARGUMENT", string.Empty },
                                     { "__LASTFOCUS", string.Empty },
                                     { "PageLoadedHiddenTxtBox", "Set" },
                                     {
                                         "__VIEWSTATE",
                                         signinPageDocument.QuerySelector<IElement>("#__VIEWSTATE")?.Attributes["value"]
                                             ?.Value
                                     },
                                     {
                                         "__EVENTVALIDATION",
                                         signinPageDocument.QuerySelector<IElement>("#__EVENTVALIDATION")
                                             ?.Attributes["value"]?.Value
                                     },
                                     { "ctl00$taxes$listCountries", "54" },
                                     { "Language", "es-MX" },
                                     { "CurrentLanguage", "es-MX" },
                                     { "Currency", string.Empty },
                                     { "ctl00$cphPage$Login$UserName", username },
                                     { "ctl00$cphPage$Login$Password", password },
                                     { "ctl00$cphPage$Login$LoginButton", "Entrar" }
                                 };
            }
            catch (Exception e)
            {
                Log.Error(e, "Error building sign in parameters");
            }

            return parameters;
        }

        private static async Task<string> DownloadCaptchaAsync(HttpClient httpClient, string captchaUrl)
        {
            string captchaFilePath = null;
            try
            {
                byte[] bytes;
                var stream = await httpClient.GetStreamAsync(captchaUrl);
                await using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    bytes = memoryStream.ToArray();
                }

                if (!Directory.Exists("captchas"))
                {
                    Directory.CreateDirectory("captchas");
                }

                var newGuid = Guid.NewGuid();
                captchaFilePath = $"captchas/{newGuid}.jpg";
                File.WriteAllBytes(captchaFilePath, bytes);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error retrieving captcha with url '{Url}'", captchaUrl);
            }

            return captchaFilePath;
        }

        private static string GetCaptchaText(string captchaFilePath)
        {
            string captchaText = null;
            Pix captcha = null;
            try
            {
                captcha = Pix.LoadFromFile(captchaFilePath);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error loading captcha file");
            }

            if (captcha != null)
            {
                var grayCaptcha = captcha.ConvertRGBToGray();
                var binarizedCaptcha = grayCaptcha.BinarizeSauvolaTiled(10, 0.75f, 1, 2);

                var engine = new TesseractEngine(Path.GetFullPath("tessdata"), "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890");
                var page = engine.Process(binarizedCaptcha, PageSegMode.SparseText);

                captchaText = page.GetText();
                captchaText = Regex.Replace(captchaText, @"\s+", string.Empty);
            }

            return captchaText;
        }

        private async Task<CookieCollection> GetCookieCollectionAsync(string url)
        {
            var cookieCollection = new CookieCollection();
            
            var storeSlug = UriHelper.GetStoreSlug(url);
            this._loginCookies.TryGetValue(storeSlug, out var storedCookieCollection);
            if (storedCookieCollection == null || !storedCookieCollection.ContainsKey("ShopMSAuth"))
            {
                storedCookieCollection = await this.GetCookiesCollectionFromCacheAsync(url);
            }

            if (storedCookieCollection != null && storedCookieCollection.ContainsKey("ShopMSAuth"))
            {
                cookieCollection.AddRange(storedCookieCollection.Values);
            }
            else
            {
                InvalidateCookies(url);
            }

            return cookieCollection;
        }

        private async Task<Dictionary<string, Cookie>> GetCookiesCollectionAsync(string url)
        {
            Dictionary<string, Cookie> cookieCollection;
            var antiScrappingCookie = await this.ReadAntiScrappingCookieAsync();
            if (url.EndsWith("stores.json"))
            {
                cookieCollection = new Dictionary<string, Cookie>();
                if (antiScrappingCookie != null)
                {
                    cookieCollection.Add(antiScrappingCookie.Name, antiScrappingCookie);
                }
            }
            else
            {
                var credentialsConfigurationSection = this._configuration.GetSection("Credentials");
                var username = credentialsConfigurationSection?["Username"];
                var password = credentialsConfigurationSection?["Password"];
                if (!string.IsNullOrWhiteSpace(username) && username != "%USERNAME%"
                                                         && !string.IsNullOrWhiteSpace(password))
                {
                    cookieCollection = await this.GetLoginCookiesAsync(url);
                }
                else
                {
                    Log.Warning("Credentials were not specified in the configuration file.");
                    cookieCollection = await this.LoadFromCookiesTxtAsync(
                                           antiScrappingCookie,
                                           url.EndsWith("stores.json"));
                }
            }

            return cookieCollection;
        }

        private async Task<Dictionary<string, Cookie>> GetCookiesCollectionFromCacheAsync(string url)
        {
            var storeSlug = UriHelper.GetStoreSlug(url);
            try
            {
                if (storeSlug != "/" && !this._loginCookies.ContainsKey(storeSlug))
                {
                    var cookieFilePath = $"data/{storeSlug}.json";
                    if (File.Exists(cookieFilePath))
                    {
                        Log.Information("Deserializing cookies from {Path}.", cookieFilePath);
                        var readAllText = await File.ReadAllTextAsync(cookieFilePath, Encoding.UTF8);
                        var cookies = JsonConvert.DeserializeObject<Dictionary<string, Cookie>>(readAllText);
                        if (cookies.ContainsKey("ShopMSAuth"))
                        {
                            return cookies;
                        }

                        // cookies.Remove("uid");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error deserializing cookies.");
            }

            return await this.GetCookiesCollectionAsync(url);
        }

        private Task<Dictionary<string, Cookie>> GetLoginCookiesAsync(string url)
        {
            Dictionary<string, Cookie> cookieCollection;
            var storeSlug = UriHelper.GetStoreSlug(url);
            lock (this._syncObj)
            {
                this._loginCookies.TryGetValue(storeSlug, out cookieCollection);
            }

            return Task.FromResult(cookieCollection);
        }

        private async Task<Dictionary<string, Cookie>> LoadFromCookiesTxtAsync(Cookie antiScrappingCookie, bool keepId)
        {
            var cookieCollection = new Dictionary<string, Cookie>();
            var cookiesFile = "data/cookies.txt";
            if (File.Exists(cookiesFile))
            {
                var readAllText = Enumerable.Where(
                    await File.ReadAllLinesAsync(cookiesFile),
                    s => !s.TrimStart().StartsWith("#"));
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

                            if (name != "SRVNAME" && (name != "uid" || keepId))
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

            if (antiScrappingCookie != null)
            {
                cookieCollection.Remove(antiScrappingCookie.Name);
                cookieCollection.Add(antiScrappingCookie.Name, antiScrappingCookie);
            }

            return cookieCollection;
        }

        private async Task<Dictionary<string, Cookie>> LoginAsync(
            Cookie antiScrappingCookie,
            string url,
            string username,
            string password,
            bool unattended)
        {
            // TODO: Improve this.
            var storeSlug = UriHelper.GetStoreSlug(url);
            var storeCaptchaFilePath = $"captchas/{storeSlug}.jpg";
            var storeCaptchaSolutionFilePath = $"captchas/{storeSlug}.txt";

            Log.Information("Authenticating in TuEnvio as {username}", username);

            var signInUrl = url.Replace("/Products?depPid=0", "/signin.aspx");
            var captchaUrl = url.Replace("/Products?depPid=0", "/captcha.ashx");

            var cookieContainer = new CookieContainer();

            string captchaFilePath = null;
            var captchaText = string.Empty;

            var isAuthenticated = false;
            var attempts = 0;
            CookieCollection httpHandlerCookieCollection = null;
            do
            {
                attempts++;

                var httpMessageHandler = new HttpClientHandler { CookieContainer = cookieContainer };
                if (antiScrappingCookie != null)
                {
                    cookieContainer.Add(ScraperConfigurations.CookieCollectionUrl, antiScrappingCookie);
                }

                var httpClient = new HttpClient(httpMessageHandler)
                                     {
                                         Timeout = ScraperConfigurations.HttpClientTimeout
                                     };
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "user-agent",
                    ScraperConfigurations.GetSupportedAgent());

                var signinPageContent = string.Empty;
                try
                {
                    var httpResponseMessage = await httpClient.GetCaptchaSaveAsync(signInUrl);
                    if (httpResponseMessage?.Content != null)
                    {
                        signinPageContent = await httpResponseMessage.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error retrieving sign in page with url '{Url}'", signInUrl);
                }

                Dictionary<string, string> signInParameters = null;
                try
                {
                    signInParameters = await BuildSignInParametersAsync(username, password, signinPageContent);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error building sign in parameters for '{Url}'", signInUrl);
                }

                if (signInParameters != null)
                {
                    captchaFilePath = await DownloadCaptchaAsync(httpClient, captchaUrl);
                    if (!string.IsNullOrWhiteSpace(captchaFilePath) && File.Exists(captchaFilePath))
                    {
                        if (unattended)
                        {
                            captchaText = GetCaptchaText(captchaFilePath);
                        }
                        else
                        {
                            File.Delete(storeCaptchaSolutionFilePath);
                            File.Copy(captchaFilePath, storeCaptchaFilePath, true);
                            while (!File.Exists(storeCaptchaSolutionFilePath))
                            {
                                await Task.Delay(1000);
                            }

                            captchaText = await File.ReadAllTextAsync(storeCaptchaSolutionFilePath);

                            File.Delete(storeCaptchaFilePath);
                            File.Delete(storeCaptchaSolutionFilePath);
                        }

                        if (!string.IsNullOrWhiteSpace(captchaText))
                        {
                            signInParameters.Add("ctl00$cphPage$Login$capcha", captchaText);
                            try
                            {
                                await httpClient.PostAsync(signInUrl, new FormUrlEncodedContent(signInParameters));
                                httpHandlerCookieCollection =
                                    cookieContainer.GetCookies(ScraperConfigurations.CookieCollectionUrl);
                                isAuthenticated =
                                    !string.IsNullOrWhiteSpace(httpHandlerCookieCollection["ShopMSAuth"]?.Value);
                            }
                            catch (Exception e)
                            {
                                Log.Warning(e, "Error authenticating in '{Url}'", signInUrl);
                            }
                        }
                    }

                    try
                    {
                        if (!isAuthenticated)
                        {
                            File.Delete(captchaFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error deleting captcha file '{FilePath}'", captchaFilePath);
                    }
                }
            }
            while (attempts < 5 && !isAuthenticated);

            if (isAuthenticated)
            {
                try
                {
                    File.Move(captchaFilePath, $"captchas/{captchaText}.jpg", true);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error moving captcha file {FilePath}", captchaFilePath);
                }
            }

            var cookiesCollection = new Dictionary<string, Cookie>();

            if (httpHandlerCookieCollection != null)
            {
                foreach (Cookie cookie in httpHandlerCookieCollection)
                {
                    if (!string.IsNullOrWhiteSpace(cookie.Value))
                    {
                        cookiesCollection[cookie.Name] = cookie;
                    }
                }
            }

            if (antiScrappingCookie != null)
            {
                cookiesCollection[antiScrappingCookie.Name] = antiScrappingCookie;
            }

            return cookiesCollection;
        }

        private async Task<Cookie> ReadAntiScrappingCookieAsync()
        {
            Cookie antiScrappingCookie = null;
            Log.Information("Initializing Anti-Scrapping Cookie.");
            try
            {
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "user-agent",
                    ScraperConfigurations.GetSupportedAgent());

                var requester = new HttpClientRequester(httpClient);
                var config = Configuration.Default.WithRequester(requester)
                    .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }).WithJs();

                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(ScraperConfigurations.StoresJsonUrl).WaitUntilAvailable();

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
                                Log.Warning(e, "Error retrieving the Anti-Scrapping cookie.");

                                await Task.Delay(100);
                            }
                        }

                        Log.Information(
                            "Read cookie '{CookieName}' with value '{CookieValue}'.",
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

        private async Task SyncCookiesAsync(string url, CookieCollection cookieCollection)
        {
            var storedCookieCollection = await this.GetCookiesCollectionFromCacheAsync(url);
            if (storedCookieCollection != null)
            {
                lock (storedCookieCollection)
                {
                    // if(!counts.TryGetValue(url, out var _))
                    // {
                    // counts[url] = 0;
                    // }

                    // counts[url] = (counts[url] + 1) % 2;
                    Log.Information("Synchronizing cookies for url '{Url}'.", url);

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
                        else if (storedCookie.Value != cookie.Value && cookie.TimeStamp > storedCookie.TimeStamp)
                        {
                            Log.Information(
                                "Synchronizing cookie '{CookieName}' with value '{CookieValue}' for url '{Url}'.",
                                cookie.Name,
                                cookie.Value,
                                url);

                            storedCookieCollection[cookie.Name] = cookie;
                        }
                    }

                    // if (counts[url] == 0)
                    // {
                    // storedCookieCollection.Remove("uid");
                    // }
                }
            }
        }
    }
}