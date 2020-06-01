namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;
    using Telegram.Bot.Types.Enums;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models;
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
            IRepository<User, int> userRepository,
            IRepository<Department, int> departmentRepository,
            IEntityScrapper<Department> departmentScrapper,
            IHubContext<MessagesHub> messageHubContext,
            ITelegramBotClient telegramBotClient = null)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Departments);

            var sourceChanged = false;

            var storedDepartments = departmentRepository.All().ToList();
            foreach (var storedDepartment in storedDepartments)
            {
                if (storedDepartment.IsEnabled)
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
                            transaction = PolicyHelper.WaitAndRetry().Execute(
                                () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));

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
                        transaction = PolicyHelper.WaitAndRetry().Execute(
                            () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));

                        department.Id = storedDepartment.Id;
                        department.Updated = dateTime;
                        departmentRepository.TryAddOrUpdate(
                            department,
                            nameof(Department.Added),
                            nameof(Department.Read));
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

                        var departmentDataTransferObject = department.ToDataTransferObject(true);
                        var message = JsonSerializer.Serialize(departmentDataTransferObject);
                        await messageHubContext.Clients.All.SendAsync(
                            ClientMethods.EntityChanged,
                            AlertSource.Departments,
                            message);

                        if (telegramBotClient != null)
                        {
                            var messageStringBuilder = new StringBuilder();
                            messageStringBuilder.AppendLine("*Product Set Changed*");
                            messageStringBuilder.AppendLine($"*Name:* _{departmentDataTransferObject.Name}_");
                            messageStringBuilder.AppendLine($"*Category:* _{departmentDataTransferObject.Category}_");
                            messageStringBuilder.AppendLine(
                                $"*Products Count:* _{departmentDataTransferObject.ProductsCount}_");
                            messageStringBuilder.AppendLine(
                                $"*Is Available:* _{departmentDataTransferObject.IsAvailable}_");
                            if (departmentDataTransferObject.IsAvailable
                                && departmentDataTransferObject.ProductsCount > 0)
                            {
                                messageStringBuilder.AppendLine(
                                    $"*Link:* [{departmentDataTransferObject.Url}]({departmentDataTransferObject.Url})");
                            }

                            messageStringBuilder.AppendLine($"*Store:* _{departmentDataTransferObject.Store}_");
                            var markdownMessage = messageStringBuilder.ToString();

                            var users = userRepository.Find(user => user.IsEnable).ToList();
                            foreach (var user in users)
                            {
                                try
                                {
                                    await telegramBotClient.SendTextMessageAsync(
                                        user.ChatId,
                                        markdownMessage,
                                        ParseMode.Markdown);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "Error sending message via telegram to {UserName}", user.Name);
                                }
                            }
                        }

                        Log.Information("Entity changed at source {Source}.", AlertSource.Departments);
                    }
                    else
                    {
                        Log.Information("No change detected for department '{url}'", storedDepartment.Url);
                    }
                }
            }

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Departments);
        }
    }
}