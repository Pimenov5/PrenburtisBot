using PrenburtisBot.Attributes;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
    [BotCommand("Создать опрос для переклички", BotCommandScopeType.AllChatAdministrators)]
    internal class SendPoll : BotCommandGroupFormBase, IAfterBotStartAsyncExecutable
    {
        public const string PLAYER_JOINED = "Иду";
        public const byte PLAYER_JOINED_BYTE = 48;

        public async Task<TextMessage> RenderAsync(MessageResult message)
        {
			string[] args = this.GetBotCommandParameters(message);
            int? messageThreadId = message.Message.Chat.IsForum ? message.Message.MessageThreadId : null;

            return await this.ExecuteAsync(this.API, this.Device.DeviceId, messageThreadId, args);
		}

        public async Task<TextMessage> ExecuteAsync(ITelegramBotClient botClient, ChatId chatId, int? messageThreadId, params string[] args)
        {
            if (args.Length > 2)
                throw new ArgumentException($"Невалидное количество аргументов команды: {args.Length}", nameof(args));

            double? dayCount = null;
            string? time = null;
            foreach (string arg in args)
                if (double.TryParse(arg, out double doubleValue))
                    dayCount ??= doubleValue;
                else if (TimeOnly.TryParse(arg, out TimeOnly timeOnly))
                    time ??= arg;

            string question = $"Перекличка на волейбол {(dayCount ?? 1) switch { 0 => "СЕГОДНЯ", 1 => "ЗАВТРА", _ => throw new ArgumentException($"{dayCount} не является валидным количеством дней") }}"
                + $" ({DateTime.Today.AddDays(dayCount ?? 1).ToString("dddd", CultureInfo.GetCultureInfo("ru-RU"))}){(string.IsNullOrEmpty(time) ? string.Empty : $" в {time}")}";

            int messageId = default;
            if (Session.Get(typeof(SendPoll), chatId.ToString()) is string pinnedMessageId && int.TryParse(pinnedMessageId, out messageId))
            {
                try
                {
                    await botClient.UnpinChatMessage(chatId, messageId);
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

            const string REPLY_ID_POSTFIX = "_REPLY_ID";
            Message pollMessage = await botClient.SendPoll(chatId, question, options, false, PollType.Regular, false, null, 
                Session.Get(typeof(SendPoll), chatId.ToString() + REPLY_ID_POSTFIX) is string strReplyId && int.TryParse(strReplyId, out int replyId) ? replyId : null,
                messageThreadId: messageThreadId);

            await botClient.PinChatMessage(chatId, pollMessage.MessageId);
            Session.Set(typeof(SendPoll), chatId.ToString(), pollMessage.MessageId.ToString());
            Session.TryWrite();

            Update[] updates = await botClient.GetUpdates();
            foreach (Update update in updates) 
                if (update.Message is Message messageFromUpdate && messageFromUpdate.Chat.Id == chatId && messageFromUpdate.Type == MessageType.PinnedMessage 
                    && messageFromUpdate.PinnedMessage is Message pinnedMessage && pinnedMessage.MessageId == pollMessage.MessageId)
                {
                    await botClient.DeleteMessage(chatId, messageFromUpdate.MessageId);
                    break;
                }

            return new TextMessage(string.Empty) { NavigateTo = messageId == default ? new(new Start(), Start.SET_QUIET) : new(new StopPoll(), chatId, messageId) };
        }
    }
}