using System;
using System.Collections.Generic;
using System.IO;
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

        private static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                var builder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
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

                    var problem = new CaptchaProblem(captchaProblemText, images);


                    if (problem.TrySolve(out var solutions))
                    {
                        Log.Information("Trying to solve captcha problem: {Name}", captchaProblemText);

                        var solutionText = string.Empty;
                        foreach (var solution in solutions)
                        {
                            solutionText += solution + ",";
                        }

                        var parameters = BuildReCaptchaParameters(solutionText, captchaDocument);

                        await httpClient.PostAsync(httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri,
                            new FormUrlEncodedContent(parameters));

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
                            if (captchaResolutionRequired)
                            {
                                problem.Pass();
                            }
                            else
                            {
                                problem.Fail();
                            }
                        }
                    }
                }
            }

            return httpResponseMessage;
        }

        public static async Task<HttpResponseMessage> GetCaptchaSaveAsync(this HttpClient httpClient,
            string uri)
        {
            return await httpClient.FuncCaptchaSaveAsync(async () => await httpClient.GetAsync(uri));
        }

        private static bool IsCaptchaResolutionRequired(HttpResponseMessage httpResponseMessage)
        {
            Argument.IsNotNull(() => httpResponseMessage);

            return httpResponseMessage.RequestMessage.RequestUri.AbsoluteUri.EndsWith("captcha.aspx");
        }

        private static async Task<bool> ProcessCaptchaSolutionAsync(HttpResponseMessage httpResponseMessage,
            string captchaProblem,
            string captchaSolutionPath, string[] solutions)
        {
            Argument.IsNotNull(() => httpResponseMessage);
            Argument.IsNotNullOrWhitespace(() => captchaProblem);
            Argument.IsNotNullOrEmptyArray(() => solutions);

            var captchaResolutionRequired = IsCaptchaResolutionRequired(httpResponseMessage);

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
                            var content =
                                await File.ReadAllTextAsync($"re-captchas/{captchaProblem}/{solution.Trim()}");
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

            return captchaResolutionRequired;
        }
    }
}