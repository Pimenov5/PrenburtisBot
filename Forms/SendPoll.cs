﻿using PrenburtisBot.Attributes;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
    [BotCommand("Создать опрос для переклички", BotCommandScopeType.AllChatAdministrators)]
    internal class SendPoll : BotCommandGroupFormBase
    {
        public const string PLAYER_JOINED = "Иду";
        public const byte PLAYER_JOINED_BYTE = 48;
        public async Task<TextMessage> RenderAsync(MessageResult message)
        {
            string question = $"Перекличка на волейбол ЗАВТРА ({DateTime.Today.AddDays(1).ToString("dddd", CultureInfo.GetCultureInfo("ru-RU"))})";
            if (message.BotCommandParameters is List<string> commandParameters && commandParameters.Count > 0 && TimeOnly.TryParse(commandParameters[0], out TimeOnly timeOnly))
                question += " в " + commandParameters[0];

            int messageId = default;
            if (Session.Get(typeof(SendPoll), this.Device.DeviceId.ToString()) is string pinnedMessageId && int.TryParse(pinnedMessageId, out messageId))
            {
                try
                {
                    await this.API.UnpinChatMessage(this.Device.DeviceId, messageId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            List<InputPollOption> options = [PLAYER_JOINED, "👀"];
            for (int i = 2; i <= 9; i++)
                if (Environment.GetEnvironmentVariable($"SEND_POLL_OPTION_{i}") is string item && !string.IsNullOrEmpty(item))
                    options.Insert(i - 1, item);

            Message pollMessage = await this.API.SendPoll(Device.DeviceId, question, options, false, PollType.Regular, false, 
                messageThreadId: message.Message.Chat.IsForum ? message.Message.MessageThreadId : null);

            await this.API.PinChatMessage(Device.DeviceId, pollMessage.MessageId);
            Session.Set(typeof(SendPoll), this.Device.DeviceId.ToString(), pollMessage.MessageId.ToString());
            Session.TryWrite();

            Update[] updates = await this.API.GetUpdates();
            foreach (Update update in updates) 
                if (update.Message is Message messageFromUpdate && messageFromUpdate.Chat.Id == this.Device.DeviceId && messageFromUpdate.Type == MessageType.PinnedMessage 
                    && messageFromUpdate.PinnedMessage is Message pinnedMessage && pinnedMessage.MessageId == pollMessage.MessageId)
                {
                    await this.Device.DeleteMessage(messageFromUpdate.MessageId);
                    break;
                }

            return new TextMessage(string.Empty) { NavigateTo = messageId == default ? new(new Start(), Start.SET_QUIET) : new(new StopPoll(), this.Device.DeviceId, messageId) };
        }
    }
}