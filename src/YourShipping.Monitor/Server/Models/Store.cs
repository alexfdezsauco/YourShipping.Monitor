namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json.Serialization;

    public class Store
    {
        [JsonIgnore]
        public DateTime Added { get; set; }

        public int CategoriesCount { get; set; }

        public int DepartmentsCount { get; set; }

        [Key]
        [JsonIgnore]
        public int Id { get; set; }

        public bool IsAvailable { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public DateTime Read { get; set; }

        [JsonIgnore]
        public string Sha256 { get; set; }

        [JsonIgnore]
        public DateTime Updated { get; set; }

        public string Url { get; set; }
    }
}