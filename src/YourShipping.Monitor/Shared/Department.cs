namespace YourShipping.Monitor.Shared
{
    public class Department
    {
        public bool HasChanged { get; set; }

        public int Id { get; set; }

        public string Name { get; set; }

        public int ProductsCount { get; set; }

        public string Store { get; set; }

        public string Url { get; set; }

        public bool IsStored { get; set; }

        public string Category { get; set; }
    }
}