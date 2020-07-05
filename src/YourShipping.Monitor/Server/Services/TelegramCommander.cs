namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;

    using Orc.EntityFrameworkCore;

    using Telegram.Bot;
    using Telegram.Bot.Args;
    using Telegram.Bot.Types;
    using Telegram.Bot.Types.Enums;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    using User = YourShipping.Monitor.Server.Models.User;

    public class TelegramCommander : ITelegramCommander
    {
        private readonly Regex commandPattern = new Regex(
            "/([^\\s]+).+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Dictionary<string, Func<Message, Task>> commands =
            new Dictionary<string, Func<Message, Task>>();

        private readonly IServiceProvider serviceProvider;

        private readonly ITelegramBotClient telegramBotClient;

        public TelegramCommander(ITelegramBotClient telegramBotClient, IServiceProvider serviceProvider)
        {
            this.telegramBotClient = telegramBotClient;
            this.serviceProvider = serviceProvider;

            this.commands.Add("search", this.Search);
            this.commands.Add("table", this.Table);
        }

        public void Start()
        {
            this.telegramBotClient.OnMessage += this.TelegramBotClient_OnOnMessage;
            this.telegramBotClient.StartReceiving();
        }

        private async Task ProcessAsync(Message message)
        {
            var serviceScope = this.serviceProvider.CreateScope();
            var userRepository = serviceScope.ServiceProvider.GetService<IRepository<User, int>>();

            var chatUsername = message.Chat.Username;
            var chatId = message.Chat.Id;

            var user = userRepository.Find(u => u.Name == chatUsername).FirstOrDefault();
            if (user != null)
            {
                if (!user.IsEnable)
                {
                    await this.telegramBotClient.SendTextMessageAsync(
                        message.Chat.Id,
                        $"Hi {chatUsername}. Please wait for the activation code.");
                }
                else if (message.Type == MessageType.Text)
                {
                    var match = this.commandPattern.Match(message.Text);
                    if (match.Success)
                    {
                        var command = match.Groups[1].Value;
                        if (this.commands.TryGetValue(command, out var task))
                        {
                            await task(message);
                        }
                        else
                        {
                            await this.telegramBotClient.SendTextMessageAsync(message.Chat.Id, "Try another command");
                        }
                    }
                    else
                    {
                        await this.telegramBotClient.SendTextMessageAsync(
                            message.Chat.Id,
                            "I don't understand you. Please try again");
                    }
                }
            }
            else
            {
                userRepository.Add(new User { ChatId = chatId, Name = chatUsername });
                await userRepository.SaveChangesAsync();

                await this.telegramBotClient.SendTextMessageAsync(
                    message.Chat.Id,
                    $"Welcome {chatUsername}. Your user is registered but disabled. Wait for activation code.");
            }
        }

        private async Task Search(Message message)
        {
            var storeRepository =
                this.serviceProvider.CreateScope().ServiceProvider.GetService<IRepository<Store, int>>();
            var productsScrapper = this.serviceProvider.GetService<IMultiEntityScrapper<Product>>();

            var match = Regex.Match(
                message.Text,
                @"/([^\s]+)\s+(""[^""]+""|[^\s]+)(\s+(in)\s+(.+))?",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var keywords = match.Groups[2].Value.Trim('"').Split(',');

                var found = false;
                foreach (var storedStore in storeRepository.All())
                {
                    if (storedStore.IsEnabled)
                    {
                        foreach (var keyword in keywords)
                        {
                            var url = storedStore.Url.Replace(
                                "/Products?depPid=0",
                                $"/Search.aspx?keywords={keyword}&depPid=0");

                            await foreach (var product in productsScrapper.GetAsync(url, true))
                            {
                                var messageStringBuilder = new StringBuilder();
                                messageStringBuilder.AppendLine($"*Search result for* _{keyword}_");
                                messageStringBuilder.AppendLine($"*Name:* _{product.Name}_");
                                messageStringBuilder.AppendLine($"*Price:* _{product.Price} {product.Currency}_");
                                messageStringBuilder.AppendLine($"*Link:* [{product.Url}]({product.Url})");
                                messageStringBuilder.AppendLine($"*Store:* _{product.Store}_");
                                messageStringBuilder.AppendLine($"*Department:* _{product.Department}_");
                                messageStringBuilder.AppendLine($"*Category:* _{product.DepartmentCategory}_");

                                await this.telegramBotClient.SendTextMessageAsync(
                                    message.Chat.Id,
                                    messageStringBuilder.ToString(),
                                    ParseMode.Markdown);
                                found = true;
                            }
                        }
                    }
                }

                if (!found)
                {
                    var messageStringBuilder = new StringBuilder();
                    messageStringBuilder.AppendLine($"Products not found for _{message.Text}_");
                    await this.telegramBotClient.SendTextMessageAsync(
                        message.Chat.Id,
                        messageStringBuilder.ToString(),
                        ParseMode.Markdown);
                }
            }
            else
            {
                await this.telegramBotClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "I don't understand you, please repeat");
            }
        }

        private async Task Table(Message message)
        {
            var builder = new StringBuilder();

            // builder.AppendLine("<pre>");
            builder.AppendLine("| Tables   |      Are      |  Cool |");
            builder.AppendLine("|----------|---------------|-------|");
            builder.AppendLine("| col 1 is |  left-aligned | $1600 |");
            builder.AppendLine("| col 2 is |    centered   |   $12 |");
            builder.AppendLine("| col 3 is | right-aligned |    $1 |");

            // builder.AppendLine("</pre>");
            await this.telegramBotClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Markdown);

            // await this.telegramBotClient.SendTextMessageAsync(message.Chat.Id, "   First Header  | Second Header\r\n  ------------- | -------------\r\n  Content Cell  | Content Cell\r\n  Content Cell  | Content Cell", ParseMode.Markdown);
        }

        private void TelegramBotClient_OnOnMessage(object? sender, MessageEventArgs e)
        {
            Task.Run(async () => await this.ProcessAsync(e.Message));
        }
    }
}