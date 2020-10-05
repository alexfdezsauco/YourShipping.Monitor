using System.Collections.Generic;
using Newtonsoft.Json;

namespace YourShipping.Monitor.Server.Extensions.Models
{
    public class CaptchaImage
    {
        [JsonProperty(Order = 0)]
        public string Src { get; set; }

        [JsonProperty(Order = 1)]
        public SortedSet<string> Names { get; set; } = new SortedSet<string>();
    }
}