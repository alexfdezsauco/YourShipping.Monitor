using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions
{
    public class CaptchaProblem
    {
        private readonly string _captchaProblemId;

        public CaptchaProblem(string text, SortedList<string, CaptchaImage> images)
        {
            Text = text;
            Images = images;
            var serializeObject = JsonConvert.SerializeObject(this);
            _captchaProblemId = serializeObject.ComputeSHA256();
        }

        [JsonProperty(Order = 0)]
        public string Text { get; set; }

        [JsonProperty(Order = 1)]
        public SortedList<string, CaptchaImage> Images { get; }

        public bool TrySolve(out List<string> solutionNames)
        {
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{_captchaProblemId}";
            var captchaProblemFilePath = $"{captchaEncodedProblemDirectoryPath}/problem";
            var captchaProblemSolutionFilePath = $"{captchaEncodedProblemDirectoryPath}/solution";

            solutionNames = null;

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
                solutionNames = new List<string>();
                var solutionFileLines = File.ReadAllLines(captchaProblemSolutionFilePath);
                foreach (var solutionFileLine in solutionFileLines)
                {
                    if (Images.TryGetValue(solutionFileLine, out var captchaImage))
                    {
                        solutionNames.AddRange(captchaImage.Names);
                    }
                    else
                    {
                        Log.Warning("Incorrect solution for problem {Text} with {Id}",Text,_captchaProblemId);

                        try
                        {
                            File.Move(captchaProblemSolutionFilePath,
                                $"{captchaEncodedProblemDirectoryPath}/!solution");
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error renaming solution file.");
                        }

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

            var captchaEncodedProblemDirectoryPath = $"re-captchas/{_captchaProblemId}";
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
                    Log.Warning(e, "Error deleting solution alert file.");
                }
            }
        }

        public void Fail()
        {
            var captchaEncodedProblemDirectoryPath = $"re-captchas/{_captchaProblemId}";
            var captchaProblemSolutionFileVerifiedPath = $"{captchaEncodedProblemDirectoryPath}/solution-verified";
            var solutionAlertFile = $"{captchaEncodedProblemDirectoryPath}/solution-alert";

            if (File.Exists(captchaProblemSolutionFileVerifiedPath))
            {
                Log.Warning("I'm not human for captcha problem: {Text} but solution is verified. {Id}", Text,
                    _captchaProblemId);
            }
            else
            {
                Log.Warning("I'm not human for captcha problem: {Text}. {Id}", Text, _captchaProblemId);

                File.Create(solutionAlertFile);
            }
        }
    }
}