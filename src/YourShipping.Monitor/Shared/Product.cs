namespace YourShipping.Monitor.Shared
{
    public class Product
    {
        public string Currency { get; set; }

        public bool HasChanged { get; set; }

        public int Id { get; set; }

        public bool IsAvailable { get; set; }

        public bool IsStored { get; set; }

        public string Name { get; set; }

        public float Price { get; set; }

        public string Store { get; set; }

        public string Url { get; set; }

        public string Department { get; set; }

        public string DepartmentCategory { get; set; }
    }
}