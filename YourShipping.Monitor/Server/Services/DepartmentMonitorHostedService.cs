namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Threading.Tasks;

    using BlazorApp6.Server;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;
    using Product = YourShipping.Monitor.Server.Models.Product;

    public sealed class DepartmentMonitorHostedService : TimedHostedServiceBase
    {
        public DepartmentMonitorHostedService(
            ILogger<DepartmentMonitorHostedService> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        [Execute]
        public async Task Execute(
            IRepository<Department, int> departmentRepository,
            IEntityScrapper<Department> departmentScrapper,
            IHubContext<MessagesHub> messageHubContext)
        {
            this.Logger.LogInformation("Running Departments Monitor.");

            var domainChanged = false;
            foreach (var storedDepartment in departmentRepository.All())
            {
                var dateTime = DateTime.Now;
                var department = await departmentScrapper.GetAsync(storedDepartment.Url);
                if (department != null)
                {
                    var hasChanged = department.Name != storedDepartment.Name
                                     || department.Store != storedDepartment.Store
                                     || department.ProductsCount != storedDepartment.ProductsCount;

                    if (hasChanged)
                    {
                        if (!domainChanged)
                        {
                            domainChanged = department.ProductsCount > 0;
                        }

                        department.Id = storedDepartment.Id;
                        department.Updated = dateTime;
                        departmentRepository.TryAddOrUpdate(department, nameof(Product.Added), nameof(Product.Read));
                    }
                }
            }

            await departmentRepository.SaveChangesAsync();
            if (domainChanged)
            {
                this.Logger.LogInformation("Departments change detected");
                await messageHubContext.Clients.All.SendAsync("DomainChanged", AlertSource.Departments);
            }
            else
            {
                this.Logger.LogInformation("No Departments change detected");
            }
        }
    }
}