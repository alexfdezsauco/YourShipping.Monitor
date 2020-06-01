namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json.Serialization;

    public class Product
    {
        [JsonIgnore]
        public DateTime Added { get; set; }

        public string Currency { get; set; }

        public string Department { get; set; }

        public string DepartmentCategory { get; set; }

        [Key]
        [JsonIgnore]
        public int Id { get; set; }

        public bool IsAvailable { get; set; }

        [JsonIgnore]
        public bool IsEnabled { get; set; }

        public string Name { get; set; }

        public float Price { get; set; }

        [JsonIgnore]
        public DateTime Read { get; set; }

        [JsonIgnore]
        public string Sha256 { get; set; }

        public string Store { get; set; }

        [JsonIgnore]
        public DateTime Updated { get; set; }

        public string Url { get; set; }
    }
}