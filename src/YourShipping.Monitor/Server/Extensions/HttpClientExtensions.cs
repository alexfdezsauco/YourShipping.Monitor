using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Catel;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions
{
    public static class HttpClientExtensions
    {
        private static readonly object syncObj = new object();

        private static FieldInfo _fieldInfo;

        static HttpClientExtensions()
        {
            lock (syncObj)
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
            lock (syncObj)
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
                    {"ctl00$cphPage$ctl00$seleccion", solution},
                    {"ctl00$cphPage$ctl00$Button1", "Enviar respuesta"}
                };
            }
            catch (Exception e)
            {
                Log.Error(e, "Error building sign in parameters");
            }

            return parameters;
        }

        // private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public static async Task<HttpResponseMessage> CaptchaSaveTaskAsync(this HttpClient httpClient,
            Func<HttpClient, Task<HttpResponseMessage>> httpClientTask)
        {
            var browsingContext = BrowsingContext.New(Configuration.Default);

            // await semaphoreSlim.WaitAsync();
            var httpResponseMessage = await httpClientTask(httpClient);
            // semaphoreSlim.Release();

            var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
            var captchaResolutionRequired = requestUriAbsoluteUri.EndsWith("captcha.aspx");
            while (captchaResolutionRequired)
            {
                var captchaContent = await httpResponseMessage.Content.ReadAsStringAsync();
                var captchaDocument = await browsingContext.OpenAsync(req => req.Content(captchaContent));
                var captchaProblem = captchaDocument.QuerySelector<IElement>("#ctl00_cphPage_ctl00_enunciado > b")
                    .Text();
                var captchaProblemPath = $"re-captchas/{captchaProblem}";
                var captchaSolutionPath = $"re-captchas/{captchaProblem}/solution";

                if (!Directory.Exists(captchaProblemPath))
                {
                    Log.Warning("New unresolved captcha problem: {Name}", captchaProblem);

                    Directory.CreateDirectory(captchaProblemPath);
                    var querySelector = captchaDocument.QuerySelectorAll<IElement>(
                        "#mainPanel > div > div > div.span10.offset1 > div:nth-child(2) > div > div > div > a > img:nth-child(2)");
                    foreach (var element in querySelector)
                    {
                        var name = element.Attributes["name"].Value;
                        var src = element.Attributes["src"].Value;
                        var combine = Path.Combine(captchaProblemPath, name);
                        File.WriteAllText(combine, src);
                        var bytes = Convert.FromBase64String(src.Substring("data:image/png;base64,%20".Length));
                        File.WriteAllBytes(combine + ".png", bytes);
                    }

                    File.WriteAllText($"re-captchas/{captchaProblem}/!solution", string.Empty);
                    captchaResolutionRequired = false;
                }
                else if (File.Exists(captchaSolutionPath))
                {
                    Log.Information("Trying to solve captcha problem: {Name}", captchaProblem);

                    var solution = File.ReadAllText(captchaSolutionPath).Trim() + ",";
                    var parameters = BuildReCaptchaParameters(solution, captchaDocument);
                    await httpClient.PostAsync(requestUriAbsoluteUri, new FormUrlEncodedContent(parameters));

                    // await semaphoreSlim.WaitAsync();
                    httpResponseMessage = await httpClientTask(httpClient);
                    // semaphoreSlim.Release();

                    requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
                    captchaResolutionRequired = requestUriAbsoluteUri.EndsWith("captcha.aspx");
                    if (!captchaResolutionRequired)
                    {
                        Log.Information("I'm human for captcha problem: {Name}", captchaProblem);
                    }
                    else
                    {
                        Log.Warning("I'm not human for captcha problem: {Name}", captchaProblem);
                    }
                }
            }

            return httpResponseMessage;
        }
    }
}