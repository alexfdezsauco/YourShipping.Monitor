namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class Product
    {
        public DateTime Added { get; set; }

        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public float Price { get; set; }

        public DateTime Read { get; set; }

        public DateTime Updated { get; set; }

        public string Url { get; set; }

        public string Currency { get; set; }

        public string Store { get; set; }

        public bool IsAvailable { get; set; }
    }
}