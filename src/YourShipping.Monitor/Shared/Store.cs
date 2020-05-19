namespace YourShipping.Monitor.Shared
{
    public class Store
    {
        public float CategoriesCount { get; set; }

        public float DepartmentsCount { get; set; }

        public bool HasChanged { get; set; }

        public int Id { get; set; }

        public bool IsAvailable { get; set; }

        public string Name { get; set; }

        public string Url { get; set; }

        public bool IsStored { get; set; }
    }
}