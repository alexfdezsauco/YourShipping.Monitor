using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
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

        public static async Task<HttpResponseMessage> CaptchaSaveTaskAsync(this HttpClient httpClient,
            Func<HttpClient, Task<HttpResponseMessage>> httpClientTask)
        {
            var browsingContext = BrowsingContext.New(Configuration.Default);

            HttpResponseMessage httpResponseMessage = null;
            try
            {
                httpResponseMessage = await httpClientTask(httpClient);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error executing HttpClient task");
            }

            if (httpResponseMessage != null)
            {
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

                        var solutionText = File.ReadAllText(captchaSolutionPath).Trim(' ', ',');
                        var parameters = BuildReCaptchaParameters(solutionText, captchaDocument);
                        await httpClient.PostAsync(requestUriAbsoluteUri, new FormUrlEncodedContent(parameters));

                        try
                        {
                            httpResponseMessage = await httpClientTask(httpClient);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error executing HttpClient task");
                        }

                        if (httpResponseMessage != null)
                        {
                            await ProcessCaptchaSolutionAsync(httpResponseMessage, captchaProblem, captchaSolutionPath,
                                solutionText.Split(','));
                        }
                    }
                }
            }

            return httpResponseMessage;
        }

        private static async Task ProcessCaptchaSolutionAsync(HttpResponseMessage httpResponseMessage,
            string captchaProblem,
            string captchaSolutionPath, string[] solutions)
        {
            Argument.IsNotNull(() => httpResponseMessage);
            Argument.IsNotNullOrWhitespace(() => captchaProblem);
            Argument.IsNotNullOrEmptyArray(() => solutions);

            var requestUriAbsoluteUri = httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri;
            var captchaResolutionRequired = requestUriAbsoluteUri.EndsWith("captcha.aspx");

            var solutionVerifiedFilePath = captchaSolutionPath + "-verified";
            var solutionWarningFilePath = captchaSolutionPath + "-warning";

            if (!captchaResolutionRequired)
            {
                await SemaphoreSlim.WaitAsync();

                if (!File.Exists(solutionVerifiedFilePath))
                {
                    try
                    {
                        var streamWriter = new StreamWriter(solutionVerifiedFilePath) {AutoFlush = true};
                        foreach (var solution in solutions)
                        {
                            var content = await File.ReadAllTextAsync($"re-captchas/{captchaProblem}/{solution.Trim()}");
                            await streamWriter.WriteLineAsync(content.Trim());
                        }

                        await streamWriter.FlushAsync();
                        streamWriter.Close();

                        if (File.Exists(solutionWarningFilePath))
                        {
                            File.Delete(solutionWarningFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error creating solution verification file.", captchaProblem);
                    }
                }

                SemaphoreSlim.Release();

                Log.Information("I'm human for captcha problem: {Name}", captchaProblem);
            }
            else if (!File.Exists(solutionVerifiedFilePath) && !File.Exists(solutionWarningFilePath))
            {
                await SemaphoreSlim.WaitAsync();

                if (!Strikes.TryGetValue(captchaProblem, out _))
                {
                    Strikes[captchaProblem] = 1;
                }
                else
                {
                    Strikes[captchaProblem]++;
                }

                var strikesCount = Strikes[captchaProblem];
                Log.Warning("I'm not human for captcha problem: {Name}. Strikes: {Count}",
                    captchaProblem,
                    strikesCount);

                if (strikesCount == 3 && !File.Exists(solutionWarningFilePath))
                {
                    try
                    {
                        File.Create(solutionWarningFilePath);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e,
                            "Error creating warning file for captcha problem {CaptchaProblem}.",
                            captchaProblem);
                    }
                }

                SemaphoreSlim.Release();
            }
        }
    }
}