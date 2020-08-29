namespace YourShipping.Monitor.Server.Models
{
    using System.ComponentModel.DataAnnotations;

    using Newtonsoft.Json;

    public class User
    {
        public long ChatId { get; set; }

        [Key]
        [System.Text.Json.Serialization.JsonIgnore]
        public int Id { get; set; }

        public bool IsEnable { get; set; }

        public string Name { get; set; }
    }
}