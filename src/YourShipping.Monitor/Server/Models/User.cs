namespace YourShipping.Monitor.Server.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.Text.Json.Serialization;

    public class User
    {
        public long ChatId { get; set; }

        [Key]
        [JsonIgnore]
        public int Id { get; set; }

        public bool IsEnable { get; set; }

        public string Name { get; set; }
    }
}