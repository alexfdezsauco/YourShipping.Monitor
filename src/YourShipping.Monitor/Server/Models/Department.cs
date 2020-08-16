namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using Newtonsoft.Json;

    public class Department
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Added { get; set; }

        [JsonProperty(Order = 0)]
        public string Category { get; set; }

        [Key]
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }

        [JsonProperty(Order = 1)]
        public bool IsAvailable { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonProperty(Order = 2)]

        public string Name { get; set; }

        [NotMapped]
        [JsonProperty(Order = 3)]

        // [JsonIgnore]
        public SortedList<string, Product> Products { get; set; } = new SortedList<string, Product>();

        [JsonProperty(Order = 4)]
        public int ProductsCount { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Read { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Sha256 { get; set; }

        [JsonProperty(Order = 5)]
        public string Store { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Updated { get; set; }

        [JsonProperty(Order = 6)]
        public string Url { get; set; }
    }
}