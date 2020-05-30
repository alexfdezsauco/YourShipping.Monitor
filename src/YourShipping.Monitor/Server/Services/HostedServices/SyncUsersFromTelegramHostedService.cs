namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;
    using Telegram.Bot.Types;

    using YourShipping.Monitor.Server.Services.Attributes;

    using User = YourShipping.Monitor.Server.Models.User;

    public sealed class SyncUsersFromTelegramHostedService : TimedHostedServiceBase
    {
        public SyncUsersFromTelegramHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        [Execute]
        public async Task ExecuteAsync(IRepository<User, int> userRepository, ITelegramBotClient telegramBotClient = null)
        {
            if (telegramBotClient == null)
            {
                Log.Information("TelegramBotClient is not registered.");

                return;
            }

            Log.Information("Synchronizing users from Telegram.");

            Update[] updates = null;
            try
            {
                updates = await telegramBotClient.GetUpdatesAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error retrieving updates from telegram");
            }

            var users = updates
                ?.Select(update => new User { ChatId = update.Message.Chat.Id, Name = update.Message.Chat.Username })
                .Where(user => !string.IsNullOrWhiteSpace(user.Name)).Distinct(EqualityComparer<User>.Default);

            if (users != null)
            {
                foreach (var user in users)
                {

                    var storedUser = userRepository.Find(u => u.Name == user.Name).FirstOrDefault();
                    if (storedUser != null)
                    {
                        user.Id = storedUser.Id;
                    }

                    var transaction = userRepository.BeginTransaction(IsolationLevel.Serializable);
                    userRepository.TryAddOrUpdate(user, nameof(User.IsEnable));
                    await userRepository.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            }

            Log.Information("User synchronization from telegram completed");
        }
    }
}