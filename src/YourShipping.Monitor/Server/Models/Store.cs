using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace YourShipping.Monitor.Server.Models
{
    public class Store
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Added { get; set; }

        [JsonProperty(Order = 0)]
        public int CategoriesCount { get; set; }

        [JsonProperty(Order = 1)]
        public int DepartmentsCount { get; set; }

        [Key]
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }

        [JsonProperty(Order = 2)]
        public bool IsAvailable { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonProperty(Order = 3)]
        public string Name { get; set; }

        [JsonProperty(Order = 4)]
        public string Province { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Read { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Sha256 { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime Updated { get; set; }

        [JsonProperty(Order = 5)]
        public string Url { get; set; }

        [JsonProperty(Order = 6)]
        public bool HasProductsInCart { get; set; }
    }
}