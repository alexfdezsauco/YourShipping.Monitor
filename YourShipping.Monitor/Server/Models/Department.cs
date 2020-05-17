namespace YourShipping.Monitor.Server.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class Department
    {
        public DateTime Added { get; set; }

        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Store { get; set; }

        public int ProductsCount { get; set; }

        public DateTime Read { get; set; }

        public DateTime Updated { get; set; }

        public string Url { get; set; }
    }
}