using PrenburtisBot.Attributes;
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

            int messageId = default;
            if (Session.Get(typeof(SendPoll), this.Device.DeviceId.ToString()) is string pinnedMessageId && int.TryParse(pinnedMessageId, out messageId))
            {
                try
                {
                    await this.Device.Client.TelegramClient.UnpinChatMessageAsync(this.Device.DeviceId, messageId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            List<string> options = [PLAYER_JOINED, "👀"];
            if (Environment.GetEnvironmentVariable("SEND_POLL_SECOND_OPTION") is string item && !string.IsNullOrEmpty(item))
                options.Insert(1, item);

            Telegram.Bot.Types.Message pollMessage = await Device.Client.TelegramClient.SendPollAsync(Device.DeviceId, question, options,
                message.Message.Chat.IsForum ?? false ? message.Message.MessageThreadId : null, isAnonymous: false, type: PollType.Regular, allowsMultipleAnswers: false);

            await Device.Client.TelegramClient.PinChatMessageAsync(Device.DeviceId, pollMessage.MessageId);
            Session.Set(typeof(SendPoll), this.Device.DeviceId.ToString(), pollMessage.MessageId.ToString());
            Session.Write();

            return new TextMessage(string.Empty) { NavigateTo = messageId == default ? new(new Start(), Start.SET_QUIET) : new(new StopPoll(), this.Device.DeviceId, messageId) };
        }
    }
}