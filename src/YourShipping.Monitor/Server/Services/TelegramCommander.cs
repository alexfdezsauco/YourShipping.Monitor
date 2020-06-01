namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Linq;
    using System.Text;
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
        private readonly IServiceProvider serviceProvider;

        private readonly ITelegramBotClient telegramBotClient;

        // private User user;
        public TelegramCommander(ITelegramBotClient telegramBotClient, IServiceProvider serviceProvider)
        {
            this.telegramBotClient = telegramBotClient;
            this.serviceProvider = serviceProvider;
        }

        public void Start()
        {
            this.telegramBotClient.OnMessage += this.TelegramBotClient_OnOnMessage;
            this.telegramBotClient.StartReceiving();
        }

        private async Task ProcessAsync(Message message)
        {
            var unitOfWork = this.serviceProvider.GetService<IUnitOfWork>();
            var productsScrapper = this.serviceProvider.GetService<IMultiEntityScrapper<Product>>();
            var userRepository = unitOfWork.GetRepository<User, int>();
            var storeRepository = unitOfWork.GetRepository<Store, int>();

            var chatUsername = message.Chat.Username;

            var user = userRepository.Find(u => u.Name == chatUsername && u.IsEnable).FirstOrDefault();
            if (user != null)
            {
                if (message.Type == MessageType.Text)
                {
                    // Log.Information(
                    // "Location: {Latitude}, {Longitude}",
                    // message?.Location?.Latitude,
                    // message?.Location?.Longitude);
                    var splitText = message.Text.Split(' ');
                    if (splitText.Length > 1)
                    {
                        var trim = splitText[1].Trim();
                        var keywordList = new[] { trim };

                        var found = false;
                        foreach (var storedStore in storeRepository.All())
                        {
                            if (storedStore.IsEnabled)
                            {
                                foreach (var keyword in keywordList)
                                {
                                   
                                    var url = storedStore.Url.Replace(
                                        "/Products?depPid=0",
                                        $"/Search.aspx?keywords={keyword}&depPid=0");
                                    
                                    await foreach (var product in productsScrapper.GetAsync(url, true))
                                    {
                                        var messageStringBuilder = new StringBuilder();
                                        messageStringBuilder.AppendLine($"*Search result for* _{keyword}_");
                                        messageStringBuilder.AppendLine($"*Name:* _{product.Name}_");
                                        messageStringBuilder.AppendLine(
                                            $"*Price:* _{product.Price} {product.Currency}_");
                                        messageStringBuilder.AppendLine($"*Link:* [{product.Url}]({product.Url})");
                                        messageStringBuilder.AppendLine($"*Store:* _{product.Store}_");
                                        messageStringBuilder.AppendLine($"*Department:* _{product.Department}_");
                                        messageStringBuilder.AppendLine(
                                            $"*Department Category:* _{product.DepartmentCategory}_");

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
                            messageStringBuilder.AppendLine($"*Products not found for command* {message.Text}");
                            await this.telegramBotClient.SendTextMessageAsync(
                                message.Chat.Id,
                                messageStringBuilder.ToString(),
                                ParseMode.Markdown);
                        }

                    }

                }
            }
        }

        private void TelegramBotClient_OnOnMessage(object? sender, MessageEventArgs e)
        {
            Task.Run(async () => await this.ProcessAsync(e.Message));
        }
    }
}