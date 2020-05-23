namespace YourShipping.Monitor.Server.Models.Extensions
{
    using Department = YourShipping.Monitor.Server.Models.Department;

    public static class DepartmentExtensions
    {
        public static Shared.Department ToDataTransferObject(this Department department, bool hasChanged = false, bool stored = true)
        {
            return new Shared.Department
                       {
                           Id = department.Id,
                           Url = department.Url,
                           Store = department.Store,
                           Name = department.Name,
                           Category = department.Category,
                           HasChanged = hasChanged,
                           IsAvailable = department.IsAvailable,
                           IsStored = stored,
                           ProductsCount = department.ProductsCount
                       };
        }
    }
}