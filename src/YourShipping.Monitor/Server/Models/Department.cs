namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Text.Json.Serialization;

    public class Department
    {
        [JsonIgnore]
        public DateTime Added { get; set; }

        [JsonIgnore]
        public bool IsEnabled { get; set; }

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

        [NotMapped]
        // [JsonIgnore]
        public SortedList<string, Product> Products { get; set; } = new SortedList<string, Product>();
    }
}