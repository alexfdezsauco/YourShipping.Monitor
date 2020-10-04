using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Catel;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions
{
    public static class HttpClientExtensions
    {
        private static readonly object SyncObj = new object();

        private static FieldInfo _fieldInfo;

        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private static readonly Dictionary<string, int> Strikes = new Dictionary<string, int>();

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

        private static Dictionary<string, string> BuildReCaptchaParameters(string solution,
            IDocument captchaPageDocument)
        {
            Argument.IsNotNullOrWhitespace(() => solution);
            Argument.IsNotNull(() => captchaPageDocument);

            Dictionary<string, string> parameters = null;
            try
            {
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
                    {"ctl00$cphPage$ctl00$seleccion", solution.Trim(' ', ',') + ","},
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


        private static async Task<HttpResponseMessage> FuncCaptchaSaveAsync(this HttpClient httpClient,
            Func<Task<HttpResponseMessage>> httpResponseMessageFunction)
        {
            var browsingContext = BrowsingContext.New(Configuration.Default);

            HttpResponseMessage httpResponseMessage = null;
            try
            {
                httpResponseMessage = await httpResponseMessageFunction();
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
                    var captchaProblemText = captchaDocument.QuerySelector<IElement>("#ctl00_cphPage_ctl00_enunciado > b")
                        .Text();


                    var selectorAll = captchaDocument.QuerySelectorAll<IElement>(
                        "#mainPanel > div > div > div.span10.offset1 > div:nth-child(2) > div > div > div > a > img:nth-child(2)");



                    SortedList<string, CaptchaImage> images = new SortedList<string, CaptchaImage>();
                    foreach (var element in selectorAll)
                    {
                        var src = element.Attributes["src"].Value;
                        var name = element.Attributes["name"].Value;
                        var key = src.ComputeSHA256();

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
                    if (captchaProblem.TrySolve(out var solutions))
                    {
                        Log.Information("Trying to solve captcha problem: {Name}", captchaProblemText);

                        var solutionText = solutions.Aggregate(string.Empty, (current, solution) => current + solution + ",");
                        var parameters = BuildReCaptchaParameters(solutionText, captchaDocument);

                        try
                        {
                            var url = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
                            await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error solving captcha {Text} with {Id}", captchaProblem.Text, captchaProblem.Id);
                        }

                        try
                        {
                           
                            httpResponseMessage = await httpResponseMessageFunction();
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

            return httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri.EndsWith("captcha.aspx");
        }
    }
}