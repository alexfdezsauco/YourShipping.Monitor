using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Catel;
using Serilog;
using YourShipping.Monitor.Server.Extensions.Models;
using YourShipping.Monitor.Server.Extensions.Threading;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace YourShipping.Monitor.Server.Extensions
{
    public static class HttpClientExtensions
    {
        private static readonly object SyncObj = new object();

        private static FieldInfo _fieldInfo;

        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private static TimeSpan _timeBetweenCallsInSeconds = TimeSpan.FromSeconds(0.5);

        private static readonly Dictionary<string, StoreSemaphore> SemaphorePerStore =
            new Dictionary<string, StoreSemaphore>();

      

        static HttpClientExtensions()
        {
            lock (SyncObj)
            {
                if (_fieldInfo == null)
                {
                    _fieldInfo = typeof(HttpMessageInvoker).GetField(
                        "_handler",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }
        }

        public static HttpClientHandler GetHttpClientHandler(this HttpClient httpClient)
        {
            var fieldInfo = GetFieldInfo();
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(httpClient) as HttpClientHandler;
            }

            return null;
        }

        private static FieldInfo GetFieldInfo()
        {
            lock (SyncObj)
            {
                if (_fieldInfo == null)
                {
                    _fieldInfo = typeof(HttpMessageInvoker).GetField(
                        "_handler",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }
            }

            return _fieldInfo;
        }

        private static Dictionary<string, string> BuildReCaptchaParameters(List<string> captchaSolution,
            IDocument captchaPageDocument)
        {
            Argument.IsNotNull(() => captchaSolution);
            Argument.IsNotNull(() => captchaPageDocument);

            Dictionary<string, string> parameters = null;
            try
            {
                var captchaSolutionText = captchaSolution
                    .Aggregate(string.Empty, (current, solution) => current + solution + ",").Trim(' ', ' ');
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
                        captchaPageDocument.QuerySelector<IElement>("#__VIEWSTATE")?.Attributes["value"]
                            ?.Value
                    },
                    {
                        "__EVENTVALIDATION",
                        captchaPageDocument.QuerySelector<IElement>("#__EVENTVALIDATION")
                            ?.Attributes["value"]?.Value
                    },
                    {"ctl00$taxes$listCountries", "54"},
                    {"Language", "es-MX"},
                    {"CurrentLanguage", "es-MX"},
                    {"Currency", string.Empty},
                    {"ctl00$cphPage$ctl00$seleccion", captchaSolutionText},
                    {"ctl00$cphPage$ctl00$Button1", "Enviar respuesta"}
                };
            }
            catch (Exception e)
            {
                Log.Error(e, "Error building sign in parameters");
            }

            return parameters;
        }

        public static async Task<HttpResponseMessage> PostCaptchaSaveAsync(this HttpClient httpClient,
            string uri, HttpContent httpContent)
        {
            return await httpClient.FuncCaptchaSaveAsync(async () => await httpClient.PostAsync(uri, httpContent));
        }

        private static async Task<T> SerializeCallAsync<T>(string storeSlug, Func<Task<T>> call)
        {
            await SemaphoreSlim.WaitAsync();

            if (!SemaphorePerStore.TryGetValue(storeSlug, out var storeSemaphore))
            {
                storeSemaphore = SemaphorePerStore[storeSlug] =
                    new StoreSemaphore(storeSlug, _timeBetweenCallsInSeconds);
            }

            SemaphoreSlim.Release();

            await storeSemaphore.WaitAsync();

            try
            {
                return await call();
            }
            finally
            {
                storeSemaphore.Release();
            }
        }

        private static async Task<HttpResponseMessage> FormPostAsync(this HttpClient httpClient, string url,
            Dictionary<string, string> parameters)
        {
            var encodedItems = parameters.Select(i =>
                WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
            var encodedContent = new StringContent(string.Join("&", encodedItems), null,
                "application/x-www-form-urlencoded");

            return await httpClient.PostAsync(url, encodedContent);
        }

        public static async Task<HttpResponseMessage> FormPostCaptchaSaveAsync(this HttpClient httpClient,
            string uri, Dictionary<string, string> parameters)
        {
            return await httpClient.FuncCaptchaSaveAsync(async () => await httpClient.FormPostAsync(uri, parameters));
        }

        private static async Task<HttpResponseMessage> FuncCaptchaSaveAsync(this HttpClient httpClient,
            Func<Task<HttpResponseMessage>> httpCallAsync)
        {
            var browsingContext = BrowsingContext.New(Configuration.Default);

            HttpResponseMessage httpResponseMessage = null;
            try
            {
                // httpResponseMessage = await SerializeCallAsync(storeSlug, httpCall);
                httpResponseMessage = await httpCallAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error executing HttpClient task");
            }

            if (httpResponseMessage != null)
            {
                var captchaResolutionRequired = IsCaptchaResolutionRequired(httpResponseMessage);
                while (httpResponseMessage != null && captchaResolutionRequired)
                {
                    var captchaContent = await httpResponseMessage.Content.ReadAsStringAsync();
                    var captchaDocument = await browsingContext.OpenAsync(req => req.Content(captchaContent));
                    var captchaProblemText = captchaDocument
                        .QuerySelector<IElement>("#ctl00_cphPage_ctl00_select > b")
                        .Text();


                    var selectorAll = captchaDocument.QuerySelectorAll<IElement>(
                        "#mainPanel > div > div > div.span10.offset1 > div:nth-child(1) > div > div > div > a > img:nth-child(2)");


                    var images = new SortedList<string, CaptchaImage>();
                    foreach (var element in selectorAll)
                    {
                        var src = element.Attributes["src"].Value;
                        var name = element.Attributes["name"].Value;
                        var key = src.ComputeSha256();

                        if (images.TryGetValue(key, out var captchaImage))
                        {
                            captchaImage.Names.Add(name);
                        }
                        else
                        {
                            captchaImage = new CaptchaImage
                            {
                                Src = src
                            };

                            captchaImage.Names.Add(name);
                            images[key] = captchaImage;
                        }
                    }

                    var captchaProblem = new CaptchaProblem(captchaProblemText, images);
                    if (captchaProblem.TrySolve(out var captchaSolution))
                    {
                        Log.Information("Trying to solve captcha problem: {Name}", captchaProblemText);

                        var parameters = BuildReCaptchaParameters(captchaSolution, captchaDocument);
                        try
                        {
                            var url = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;

                            await httpClient.FormPostAsync(url, parameters);

                            // await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
                            // await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));

                            //await SerializeCallAsync(storeSlug, async () =>
                            //    {
                            //        try
                            //        {
                            //            return await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
                            //        }
                            //        catch (Exception e)
                            //        {
                            //            Log.Error(e, "Error solving captcha {Text} with {Id}", captchaProblem.Text, captchaProblem.Id);
                            //        }

                            //        return null;
                            //    }
                            //);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error solving captcha {Text} with {Id}", captchaProblem.Text,
                                captchaProblem.Id);
                        }

                        try
                        {
                            // httpResponseMessage = await SerializeCallAsync(storeSlug, httpCall);
                            httpResponseMessage = await httpCallAsync();
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error executing HttpClient task");
                        }

                        if (httpResponseMessage != null)
                        {
                            captchaResolutionRequired = IsCaptchaResolutionRequired(httpResponseMessage);
                            if (!captchaResolutionRequired)
                            {
                                captchaProblem.Pass();
                            }
                            else
                            {
                                captchaProblem.Fail();
                            }
                        }
                    }
                }
            }

            return httpResponseMessage;
        }

        public static async Task<HttpResponseMessage> GetCaptchaSaveAsync(this HttpClient httpClient, string uri)
        {
            return await httpClient.FuncCaptchaSaveAsync(async () => await httpClient.GetAsync(uri));
        }


        private static bool IsCaptchaResolutionRequired(HttpResponseMessage httpResponseMessage)
        {
            Argument.IsNotNull(() => httpResponseMessage);
            var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
            return requestUriAbsoluteUri.EndsWith("captcha.aspx") || requestUriAbsoluteUri.EndsWith("captch.aspx");
        }

        public static void Configure(IConfiguration configuration)
        {
            var timeBetweenCalls = configuration.GetSection("Http")?["TimeBetweenCallsInSeconds"];
            if (float.TryParse(timeBetweenCalls, out var timeBetweenCallsInSeconds))
            {
                _timeBetweenCallsInSeconds = TimeSpan.FromSeconds(timeBetweenCallsInSeconds);
            }
        }
    }
}