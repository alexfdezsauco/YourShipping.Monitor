namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using Blorc.PatternFly.Components.Table;

    using Newtonsoft.Json;

    public class Product
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Added { get; set; }

        [JsonProperty(Order = 0)]
        public string Currency { get; set; }

        [JsonProperty(Order = 1)]
        public string Department { get; set; }

        [JsonProperty(Order = 2)]
        public string DepartmentCategory { get; set; }

        [Key]
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }

        [JsonProperty(Order = 3)]
        public bool IsAvailable { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonProperty(Order = 4)]
        public string Name { get; set; }

        [JsonProperty(Order = 5)]
        public float Price { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Read { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Sha256 { get; set; }

        [JsonProperty(Order = 6)]
        public string Store { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Updated { get; set; }

        [JsonProperty(Order = 7)]
        public string Url { get; set; }

    }
}