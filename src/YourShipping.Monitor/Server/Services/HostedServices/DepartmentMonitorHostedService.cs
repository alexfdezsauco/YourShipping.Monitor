namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Data;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;

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
                var dateTime = DateTime.Now;
                var department = await departmentScrapper.GetAsync(storedDepartment.Url, true);
                IDbContextTransaction transaction = null;
                Log.Information("Updating scrapped department '{url}'", storedDepartment.Url);
                if (department == null)
                {
                    department = storedDepartment;
                    if (department.IsAvailable)
                    {
                        transaction = departmentRepository.BeginTransaction(IsolationLevel.Serializable);

                        department.IsAvailable = false;
                        department.Updated = dateTime;
                        department.Sha256 = JsonSerializer.Serialize(department).ComputeSHA256();

                        sourceChanged = true;
                        Log.Information(
                            "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                            department.Name,
                            department.Store,
                            department.IsAvailable);
                    }
                }
                else if (department.Sha256 != storedDepartment.Sha256)
                {
                    transaction = departmentRepository.BeginTransaction(IsolationLevel.Serializable);

                    department.Id = storedDepartment.Id;
                    department.Updated = dateTime;
                    departmentRepository.TryAddOrUpdate(department, nameof(Department.Added), nameof(Department.Read));
                    sourceChanged = true;

                    Log.Information(
                        "Department {Department} from {Store} has changed. Is Available: {IsAvailable}",
                        department.Name,
                        department.Store,
                        department.IsAvailable);
                }

                if (transaction != null)
                {
                    await departmentRepository.SaveChangesAsync();
                    await transaction.CommitAsync();
                    await messageHubContext.Clients.All.SendAsync(
                        ClientMethods.EntityChanged,
                        AlertSource.Departments,
                        JsonSerializer.Serialize(department.ToDataTransferObject(true)));

                    Log.Information("Entity changed at source {Source}.", AlertSource.Departments);
                }
                else
                {
                    Log.Information("No change detected for department '{url}'", storedDepartment.Url);
                }
            }

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Departments);
        }
    }
}