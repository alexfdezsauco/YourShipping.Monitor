namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;
    using Product = YourShipping.Monitor.Server.Models.Product;

    public sealed class DepartmentMonitorHostedService : TimedHostedServiceBase
    {
        public DepartmentMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Department, int> departmentRepository,
            IEntityScrapper<Department> departmentScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            Log.Information("Running Departments Monitor.");

            var sourceChanged = false;
            foreach (var storedDepartment in departmentRepository.All())
            {
                var dateTime = DateTime.Now;
                Department department = null;
                try
                {
                    department = await departmentScrapper.GetAsync(storedDepartment.Url);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error scrapping department '{url}'", storedDepartment.Url);
                }

                if (department != null)
                {
                    if (department.Sha256 != storedDepartment.Sha256)
                    {
                        if (!sourceChanged)
                        {
                            sourceChanged = department.ProductsCount > 0;
                        }

                        department.Id = storedDepartment.Id;
                        department.Updated = dateTime;
                        departmentRepository.TryAddOrUpdate(department, nameof(Product.Added), nameof(Product.Read));
                    }
                }
            }

            await departmentRepository.SaveChangesAsync();
            if (sourceChanged)
            {
                Log.Information("{Source} change detected", AlertSource.Departments.ToString());

                await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Departments);
            }
            else
            {
                Log.Information("No {Source} change detected", AlertSource.Departments.ToString());
            }
        }
    }
}