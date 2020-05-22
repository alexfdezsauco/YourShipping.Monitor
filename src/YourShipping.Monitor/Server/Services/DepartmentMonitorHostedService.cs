namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Department = YourShipping.Monitor.Server.Models.Department;

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
            Log.Information("Running {Source} Monitor.", AlertSource.Departments);

            var sourceChanged = false;
            foreach (var storedDepartment in departmentRepository.All())
            {
                var dateTime = DateTime.Now;
                Department department = null;
                try
                {
                    department = await departmentScrapper.GetAsync(storedDepartment.Url, true);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error scrapping department '{url}'", storedDepartment.Url);
                }

                if (department == null)
                {
                    if (storedDepartment.IsAvailable)
                    {
                        storedDepartment.IsAvailable = false;
                        storedDepartment.Updated = dateTime;
                        storedDepartment.Sha256 = JsonSerializer.Serialize(storedDepartment.IsAvailable).ComputeSHA256();
                        sourceChanged = true;

                        Log.Information("Department {Department} from {Store} has changed. Is Available: {IsAvailable}", storedDepartment.Name, storedDepartment.Store, storedDepartment.IsAvailable);
                    }
                }
                else if (department.Sha256 != storedDepartment.Sha256)
                {
                    department.Id = storedDepartment.Id;
                    department.Updated = dateTime;
                    departmentRepository.TryAddOrUpdate(department, nameof(Department.Added), nameof(Department.Read));
                    sourceChanged = true;

                    Log.Information("Department {Department} from {Store} has changed. Is Available: {IsAvailable}", department.Name, department.Store, department.IsAvailable);
                }
            }

            await departmentRepository.SaveChangesAsync();
            if (sourceChanged)
            {
                Log.Information("{Source} change detected", AlertSource.Departments);

                await messageHubContext.Clients.All.SendAsync(ClientMethods.SourceChanged, AlertSource.Departments);
            }
            else
            {
                Log.Information("No {Source} change detected", AlertSource.Departments);
            }
        }
    }
}