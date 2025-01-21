﻿using PrenburtisBot.Attributes;
using System.Globalization;
using Telegram.Bot;
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

            if (Session.Get(typeof(SendPoll), this.Device.DeviceId.ToString()) is string pinnedMessageId && int.TryParse(pinnedMessageId, out int messageId))
            {
                try
                {
                    await this.Device.Api(async (ITelegramBotClient botClient) => await botClient.UnpinChatMessageAsync(this.Device.DeviceId, messageId));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            Telegram.Bot.Types.Message pollMessage = await Device.Api(async (botClient) => await botClient.SendPollAsync(Device.DeviceId, question,
                [PLAYER_JOINED, "👀"], message.Message.Chat.IsForum ?? false ? message.Message.MessageThreadId : null,
                isAnonymous: false, type: PollType.Regular, allowsMultipleAnswers: false));

            await Device.Api(async (botClient) => await botClient.PinChatMessageAsync(Device.DeviceId, pollMessage.MessageId));
            Session.Set(typeof(SendPoll), this.Device.DeviceId.ToString(), pollMessage.MessageId.ToString());
            Session.Write();

            return new TextMessage(string.Empty).NavigateToStart();
        }
    }
}