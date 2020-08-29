namespace YourShipping.Monitor.Server.Models
{
    using System.ComponentModel.DataAnnotations;

    public class IgnoreItem
    {
        [Key]
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }

        public string Url { get; set; }
    }
}