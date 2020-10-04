using System;
using System.Collections.Generic;
using System.IO;
using Catel.Collections;
using Newtonsoft.Json;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions
{
    public class CaptchaProblem
    {
        [JsonProperty(Order = 0)]
        public string Text { get; set; }

        [JsonProperty(Order = 1)]
        public SortedList<string, CaptchaImage> Images { get; } = new SortedList<string, CaptchaImage>();

        public bool TrySolve(out List<string> solutions)
        {
            solutions = null;
            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemFilePath = $"{captchaEncodedProblemDirectoryPath}/problem";
            var captchaProblemSolutionFilePath = $"{captchaEncodedProblemDirectoryPath}/solution";

            if (!Directory.Exists(captchaEncodedProblemDirectoryPath))
            {
                Log.Warning("New unresolved captcha problem: {Name}", Text);
                Directory.CreateDirectory(captchaEncodedProblemDirectoryPath);
                File.WriteAllText(captchaProblemFilePath, Text);
                foreach (var keyValuePair in Images)
                {
                    var encodedImageContent = keyValuePair.Value.Src.Substring("data:image/png;base64,%20".Length);
                    var combine = Path.Combine(captchaEncodedProblemDirectoryPath, keyValuePair.Key);
                    var bytes = Convert.FromBase64String(encodedImageContent);
                    File.WriteAllBytes(combine + ".png", bytes);
                }

                File.Create($"{captchaEncodedProblemDirectoryPath}/!solution");
                return false;
            }

            if (File.Exists(captchaProblemSolutionFilePath))
            {
                solutions = new List<string>();
                var readAllLines = File.ReadAllLines(captchaProblemSolutionFilePath);
                foreach (var readAllLine in readAllLines)
                {
                    if (Images.TryGetValue(readAllLine, out var captchaImage))
                    {
                        solutions.AddRange(captchaImage.Names);
                    }
                    else
                    {
                        File.Move(captchaProblemSolutionFilePath, $"{captchaEncodedProblemDirectoryPath}/!solution");
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public void MarkAsResolved()
        {
            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemSolutionFilePath = $"{captchaEncodedProblemDirectoryPath}/solution-verified";
            if (!File.Exists(captchaProblemSolutionFilePath))
            {
                File.Create(captchaProblemSolutionFilePath);
            }
        }

        public void InvalidateIfNotResolved()
        {
            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemSolutionFilePath = $"{captchaEncodedProblemDirectoryPath}/solution";
            if (!File.Exists(captchaProblemSolutionFilePath))
            {
                File.Create($"{captchaEncodedProblemDirectoryPath}/solution-alert");
            }
        }
    }
}