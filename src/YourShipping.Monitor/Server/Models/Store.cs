namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class Store
    {
        public DateTime Added { get; set; }

        public int CategoriesCount { get; set; }

        public int DepartmentsCount { get; set; }

        [Key]
        public int Id { get; set; }

        public bool IsAvailable { get; set; }

        public string Name { get; set; }

        public DateTime Read { get; set; }

        public string Sha256 { get; set; }

        public DateTime Updated { get; set; }

        public string Url { get; set; }
    }
}