namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Dasync.Collections;

    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.DependencyInjection;

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

    using Store = YourShipping.Monitor.Server.Models.Store;

    public sealed class StoreMonitorHostedService : TimedHostedServiceBase
    {
        public StoreMonitorHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider, TimeSpan.FromSeconds(30))
        {
        }

        [Execute]
        public async Task ExecuteAsync(
            IRepository<Store, int> globalStoreRepository,
            IServiceProvider serviceProvider,
            IHubContext<MessagesHub> messageHubContext,
            ITelegramBotClient telegramBotClient = null)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Stores);

            var sourceChanged = false;
            var storedStores = globalStoreRepository.Find(store => store.IsEnabled).ToList();
            await storedStores.ParallelForEachAsync(
                async storedStore =>
                    {
                        var serviceScope = serviceProvider.CreateScope();
                        var serviceScopeServiceProvider = serviceScope.ServiceProvider;
                        var storeRepository = serviceScopeServiceProvider.GetService<IRepository<Store, int>>();
                        var userRepository = serviceScopeServiceProvider.GetService<IRepository<User, int>>();
                        var storeScrapper = serviceProvider.GetService<IEntityScrapper<Store>>();

                        var dateTime = DateTime.Now;
                        var store = await storeScrapper.GetAsync(storedStore.Url, true);
                        IDbContextTransaction transaction = null;
                        Log.Information("Updating scrapped store '{url}'", storedStore.Url);
                        if (store == null)
                        {
                            store = storedStore;
                            if (store.IsAvailable)
                            {
                                transaction = PolicyHelper.WaitAndRetry().Execute(
                                    () => storeRepository.BeginTransaction(IsolationLevel.Serializable));

                                store.IsAvailable = false;
                                store.Updated = dateTime;
                                store.Sha256 = JsonSerializer.Serialize(storedStore).ComputeSHA256();
                                sourceChanged = true;

                                Log.Information(
                                    "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                                    storedStore.Name,
                                    storedStore.Province,
                                    storedStore.IsAvailable);
                            }
                        }
                        else if (store.Sha256 != storedStore.Sha256)
                        {
                            transaction = PolicyHelper.WaitAndRetry().Execute(
                                () => storeRepository.BeginTransaction(IsolationLevel.Serializable));

                            store.Id = storedStore.Id;
                            store.Updated = dateTime;
                            storeRepository.TryAddOrUpdate(store, nameof(Store.Added), nameof(Store.Read));
                            sourceChanged = true;

                            Log.Information(
                                "Store {Store} from {Province} has changed. Is Available: {IsAvailable}",
                                store.Name,
                                store.Province,
                                store.IsAvailable);
                        }

                        if (transaction != null)
                        {
                            await storeRepository.SaveChangesAsync();
                            await transaction.CommitAsync();

                            var storeDataTransferObject = store.ToDataTransferObject(true);
                            var message = JsonSerializer.Serialize(storeDataTransferObject);
                            await messageHubContext.Clients.All.SendAsync(
                                ClientMethods.EntityChanged,
                                AlertSource.Stores,
                                message);

                            Log.Information("Entity changed at source {Source}.", AlertSource.Stores);

                            if (telegramBotClient != null && store.IsAvailable)
                            {
                                var messageStringBuilder = new StringBuilder();
                                messageStringBuilder.AppendLine("*Store Changed*");
                                messageStringBuilder.AppendLine($"*Name:* _{storeDataTransferObject.Name}_");
                                messageStringBuilder.AppendLine(
                                    $"*Categories Count:* _{storeDataTransferObject.CategoriesCount}_");
                                messageStringBuilder.AppendLine(
                                    $"*Departments Count:* _{storeDataTransferObject.DepartmentsCount}_");

                                messageStringBuilder.AppendLine(
                                    $"*Link:* [{storeDataTransferObject.Url}]({storeDataTransferObject.Url})");

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
                        }
                        else
                        {
                            Log.Information("No change detected for store '{url}'", storedStore.Url);
                        }
                    });

            Log.Information(
                sourceChanged ? "{Source} changes detected" : "No {Source} changes detected",
                AlertSource.Stores);
        }
    }
}