namespace YourShipping.Monitor.Server.Models.Extensions
{
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;

    public static class DepartmentExtensions
    {
        public static Shared.Department ToDataTransferObject(this Department department, bool hasChanged = false)
        {
            return new Shared.Department
                       {
                           Id = department.Id,
                           Url = department.Url,
                           Store = department.Store,
                           Name = department.Name,
                           HasChanged = hasChanged,
                           ProductsCount = department.ProductsCount
                       };
        }
    }
}