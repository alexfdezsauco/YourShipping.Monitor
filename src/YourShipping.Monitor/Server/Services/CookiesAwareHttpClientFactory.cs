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
using Catel;
using Catel.Caching;
using Catel.Collections;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Tesseract;
using YourShipping.Monitor.Server.Extensions;
using YourShipping.Monitor.Server.Services;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace YourShipping.Monitor.Server.Helpers
{
    // TODO: Improve this?
    public class CookiesAwareHttpClientFactory : ICookiesAwareHttpClientFactory
    {
        private readonly IConfiguration _configuration;

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

        public CookiesAwareHttpClientFactory(IServiceProvider provider, IConfiguration configuration)
        {
            this.provider = provider;
            _configuration = configuration;
            fileSystemWatcher = new FileSystemWatcher("data", "cookies.txt")
            {
                NotifyFilter =
                    NotifyFilters.LastWrite | NotifyFilters.CreationTime
                                            | NotifyFilters.FileName
            };

            fileSystemWatcher.Changed += (sender, args) =>
            {
                Log.Information("Cookies file changed");

                cookieCollectionCacheStorage.Clear();
            };

            fileSystemWatcher.Created += (sender, args) =>
            {
                Log.Information("Cookies file created");

                cookieCollectionCacheStorage.Clear();
            };

            fileSystemWatcher.EnableRaisingEvents = true;
        }

        public async Task<HttpClient> CreateHttpClientAsync(string url)
        {
            var httpClient = provider.GetService<HttpClient>();

            var clientHandler = httpClient.GetHttpClientHandler();
            var cookieCollection = await GetCookieCollectionAsync(url);

            clientHandler.CookieContainer.Add(ScraperConfigurations.CookieCollectionUrl, cookieCollection);

            return httpClient;
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

            cookieCollectionCacheStorage.Remove(url);
        }

        public async Task SerializeAsync()
        {
            Log.Information("Serializing cookies...");
            var urls = cookieCollectionCacheStorage.Keys.ToList();
            foreach (var url in urls)
            {
                var storedCookieCollection = await GetCookiesCollectionFromCacheAsync(url);
                lock (storedCookieCollection)
                {
                    try
                    {
                        var parts = new Url(url).Path.Split('/');
                        if (parts.Length > 1)
                        {
                            var cookiesFilePath = $"data/{parts[0]}.json";
                            Log.Information("Serializing cookies for {Path}.", cookiesFilePath);
                            var serializeObject = JsonConvert.SerializeObject(
                                storedCookieCollection,
                                Formatting.Indented);
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

        public async Task SyncCookiesAsync(string url, HttpClient httpClient)
        {
            var httpClientHandler = httpClient.GetHttpClientHandler();
            var cookieContainer = httpClientHandler.CookieContainer;
            var cookieCollection = cookieContainer.GetCookies(ScraperConfigurations.CookieCollectionUrl);
            if (!string.IsNullOrWhiteSpace(cookieCollection[".ASPXANONYMOUS"]?.Value))
            {
                Log.Warning("Session expires. Cookies will be invalidated.");

                InvalidateCookies(url);
            }
            else
            {
                await SyncCookiesAsync(url, cookieCollection);
            }
        }

        private async Task<CookieCollection> GetCookieCollectionAsync(string url)
        {
            var cookieCollection = new CookieCollection();

            var storedCookieCollection = await GetCookiesCollectionFromCacheAsync(url);
            if (storedCookieCollection != null)
            {
                lock (storedCookieCollection)
                {
                    cookieCollection.AddRange(storedCookieCollection.Values);
                }
            }

            return cookieCollection;
        }

        public async Task<Dictionary<string, Cookie>> GetCookiesCollectionAsync(string url)
        {
            Dictionary<string, Cookie> cookieCollection = null;

            var antiScrappingCookie = await ReadAntiScrappingCookieAsync();

            if (url.EndsWith("stores.json"))
            {
                cookieCollection = new Dictionary<string, Cookie> {{antiScrappingCookie.Name, antiScrappingCookie}};
            }
            else
            {
                var credentialsConfigurationSection = _configuration.GetSection("Credentials");
                var username = credentialsConfigurationSection?["Username"];
                var password = credentialsConfigurationSection?["Password"];
                if (!string.IsNullOrWhiteSpace(username) && username != "%USERNAME%" &&
                    !string.IsNullOrWhiteSpace(password))
                {
                    cookieCollection = await LoginAsync(antiScrappingCookie, url, username, password);
                }
                else
                {
                    Log.Warning("Credentials were not specified in the configuration file.");
                }

                if (cookieCollection == null || !cookieCollection.ContainsKey("ShopMSAuth"))
                {
                    cookieCollection = await LoadFromCookiesTxtAsync(antiScrappingCookie);
                }
            }

            return cookieCollection;
        }

        private async Task<Dictionary<string, Cookie>> LoginAsync(Cookie antiScrappingCookie,
            string url,
            string username, string password)
        {
            Log.Information("Authenticating in TuEnvio as {username}", username);

            var signInUrl
                = url.Replace("/Products?depPid=0", "/signin.aspx");
            var captchaUrl
                = url.Replace("/Products?depPid=0", "/captcha.ashx");

            var cookieContainer = new CookieContainer();

            string captchaFilePath = null;
            var captchaText = string.Empty;

            var isAuthenticated = false;
            var attempts = 0;
            CookieCollection httpHandlerCookieCollection = null;
            do
            {
                attempts++;

                var httpMessageHandler = new HttpClientHandler
                {
                    CookieContainer = cookieContainer
                };
                cookieContainer.Add(ScraperConfigurations.CookieCollectionUrl, antiScrappingCookie);

                var httpClient = new HttpClient(httpMessageHandler)
                {
                    Timeout = ScraperConfigurations.HttpClientTimeout
                };
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue {NoCache = true};
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("user-agent",
                    ScraperConfigurations.SupportedAgents);

                var signinPageContent = string.Empty;
                try
                {
                    var httpResponseMessage = await httpClient.GetCaptchaSaveAsync(signInUrl);
                    if (httpResponseMessage?.Content != null)
                    {
                        signinPageContent = await httpResponseMessage.Content.ReadAsStringAsync();
                    }

                    // signinPageContent = await httpClient.GetStringAsync(signInUrl);
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
                        captchaText = GetCaptchaText(captchaFilePath);
                        if (!string.IsNullOrWhiteSpace(captchaText))
                        {
                            signInParameters.Add("ctl00$cphPage$Login$capcha", captchaText);
                            try
                            {
                                await httpClient.PostAsync(
                                    signInUrl,
                                    new FormUrlEncodedContent(signInParameters));
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
            } while (attempts < 5 && !isAuthenticated);

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

            cookiesCollection[antiScrappingCookie.Name] = antiScrappingCookie;

            return cookiesCollection;
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
                captchaText = Regex.Replace(captchaText, @"\s+", "");
            }

            return captchaText;
        }

        private static async Task<Dictionary<string, string>> BuildSignInParametersAsync(string username,
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
                    {
                        "__EVENTTARGET",
                        string.Empty
                    },
                    {"__EVENTARGUMENT", string.Empty},
                    {"__LASTFOCUS", string.Empty},
                    {
                        "PageLoadedHiddenTxtBox", "Set"
                    },
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
                    {"ctl00$taxes$listCountries", "54"},
                    {"Language", "es-MX"},
                    {"CurrentLanguage", "es-MX"},
                    {"Currency", string.Empty},
                    {"ctl00$cphPage$Login$UserName", username},
                    {"ctl00$cphPage$Login$Password", password},
                    {"ctl00$cphPage$Login$LoginButton", "Entrar"}
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

        private async Task SyncCookiesAsync(string url, CookieCollection cookieCollection)
        {
            var storedCookieCollection = await GetCookiesCollectionFromCacheAsync(url);
            if (storedCookieCollection != null)
            {
                lock (storedCookieCollection)
                {
                    Log.Information("Synchronizing cookies for url '{Url}'.", url);

                    foreach (Cookie cookie in cookieCollection)
                    {
                        if (cookie.Name != "uid") // TODO: Review the implications later.
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
                    }
                }
            }
        }

        private async Task<Dictionary<string, Cookie>> GetCookiesCollectionFromCacheAsync(string url)
        {
            return await cookieCollectionCacheStorage.GetFromCacheOrFetchAsync(
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
                                var cookies = JsonConvert.DeserializeObject<Dictionary<string, Cookie>>(readAllText);
                                cookies.Remove("uid");
                                return cookies;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error deserializing cookies.");
                    }

                    return await GetCookiesCollectionAsync(url);
                });
        }

        private async Task<Dictionary<string, Cookie>> LoadFromCookiesTxtAsync(Cookie antiScrappingCookie)
        {
            var cookieCollection = new Dictionary<string, Cookie>();
            var cookiesFile = "data/cookies.txt";
            if (File.Exists(cookiesFile))
            {
                var readAllText =
                    (await File.ReadAllLinesAsync(cookiesFile)).Where(s => !s.TrimStart().StartsWith("#"));
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

                            if (name != "SRVNAME" && name != "uid")
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

        private async Task<Cookie> ReadAntiScrappingCookieAsync()
        {
            Cookie antiScrappingCookie = null;
            Log.Information("Initializing Anti-Scrapping Cookie.");
            try
            {
                var httpClient = new HttpClient {Timeout = TimeSpan.FromSeconds(60)};
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue {NoCache = true};

                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "user-agent",
                    ScraperConfigurations.GetSupportedAgent());

                var requester = new HttpClientRequester(httpClient);
                var config = Configuration.Default.WithRequester(requester)
                    .WithDefaultLoader(new LoaderOptions {IsResourceLoadingEnabled = true}).WithJs();

                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(ScraperConfigurations.StoresJsonUrl).WaitUntilAvailable();

                var content = document.Body.TextContent;
                var match = Regex.Match(content, @"Server\sError\s+406");
                if (!match.Success && !string.IsNullOrWhiteSpace(content))
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
    }
}