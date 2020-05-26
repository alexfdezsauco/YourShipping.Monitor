namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models.Extensions;
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
                var entityChanged = false;
                var dateTime = DateTime.Now;
                var department = await departmentScrapper.GetAsync(storedDepartment.Url, true);

                if (department == null)
                {
                    department = storedDepartment;
                    if (department.IsAvailable)
                    {
                        department.IsAvailable = false;
                        department.Updated = dateTime;
                        department.Sha256 = JsonSerializer.Serialize(department).ComputeSHA256();

                        sourceChanged = true;
                        entityChanged = true;

                        Log.Information(
                            "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                            department.Name,
                            department.Store,
                            department.IsAvailable);
                    }
                }
                else if (department.Sha256 != storedDepartment.Sha256)
                {
                    department.Id = storedDepartment.Id;
                    department.Updated = dateTime;
                    departmentRepository.TryAddOrUpdate(department, nameof(Department.Added), nameof(Department.Read));

                    sourceChanged = true;
                    entityChanged = true;

                    Log.Information(
                        "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                        department.Name,
                        department.Store,
                        department.IsAvailable);
                }

                if (entityChanged)
                {
                    bool error = true;
                    while (error)
                    {
                        try
                        {
                            await departmentRepository.SaveChangesAsync();
                            await messageHubContext.Clients.All.SendAsync(
                                ClientMethods.EntityChanged,
                                AlertSource.Departments,
                                JsonSerializer.Serialize(department.ToDataTransferObject(true)));

                            Log.Information("Entity changed at source {Source}.", AlertSource.Departments);

                            error = false;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error saving department '{0}'", department.Url);
                            await Task.Delay(100);
                        }
                    }
                }
            }

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Departments);
        }
    }
}