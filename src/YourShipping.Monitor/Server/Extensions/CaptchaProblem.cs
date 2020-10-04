using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions
{
    public class CaptchaProblem
    {
        public CaptchaProblem(string text, SortedList<string, CaptchaImage> images)
        {
            Text = text;
            Images = images;
        }

        [JsonProperty(Order = 0)]
        public string Text { get; set; }

        [JsonProperty(Order = 1)]
        public SortedList<string, CaptchaImage> Images { get; }

        public bool TrySolve(out List<string> solutions)
        {
            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemFilePath = $"{captchaEncodedProblemDirectoryPath}/problem";
            var captchaProblemSolutionFilePath = $"{captchaEncodedProblemDirectoryPath}/solution";

            solutions = null;

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

        public void Pass()
        {
            Log.Information("I'm human for captcha problem: {Text}", Text);

            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemSolutionFileVerifiedPath = $"{captchaEncodedProblemDirectoryPath}/solution-verified";
            var solutionAlertFile = $"{captchaEncodedProblemDirectoryPath}/solution-alert";

            if (!File.Exists(captchaProblemSolutionFileVerifiedPath))
            {
                File.Create(captchaProblemSolutionFileVerifiedPath);
            }

            if (File.Exists(solutionAlertFile))
            {
                try
                {
                    File.Delete(solutionAlertFile);
                }
                catch (Exception e)
                {
                    Log.Warning(e,"Error deleting solution alert file.");
                }
            }
        }

        public void Fail()
        {
            var serializeObject = JsonConvert.SerializeObject(this);
            var encodedCaptchaProblem = serializeObject.ComputeSHA256();
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{encodedCaptchaProblem}";
            var captchaProblemSolutionFileVerifiedPath = $"{captchaEncodedProblemDirectoryPath}/solution-verified";
            var solutionAlertFile = $"{captchaEncodedProblemDirectoryPath}/solution-alert";

            if (File.Exists(captchaProblemSolutionFileVerifiedPath))
            {
                Log.Warning("I'm not human for captcha problem: {Text} but solution is verified", Text);
            }
            else
            {
                Log.Warning("I'm not human for captcha problem: {Text}.", Text);

                File.Create(solutionAlertFile);
            }
        }
    }
}