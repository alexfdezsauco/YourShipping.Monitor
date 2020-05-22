namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json.Serialization;

    public class Department
    {
        [JsonIgnore]
        public DateTime Added { get; set; }

        [Key]
        [JsonIgnore]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Store { get; set; }

        public int ProductsCount { get; set; }

        [JsonIgnore]
        public DateTime Read { get; set; }

        [JsonIgnore]
        public DateTime Updated { get; set; }

        public string Url { get; set; }

        public bool IsAvailable { get; set; }

        [JsonIgnore]
        public string Sha256 { get; set; }

        public string Category { get; set; }
    }
}