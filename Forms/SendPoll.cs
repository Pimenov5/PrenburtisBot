using PrenburtisBot.Attributes;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using PrenburtisBot.Types;
using PrenburtisBot.Extensions;
using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Forms
{
    [BotCommand("Создать опрос для переклички", BotCommandScopeType.AllChatAdministrators)]
    internal class SendPoll : BotCommandGroupFormBase, IAfterBotStartAsyncExecutable
    {
        public const string PLAYER_JOINED = "Иду";

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

            DateOnly date = DateOnly.FromDateTime(DateTime.Today.AddDays(dayCount ?? 1));
            string question = $"Перекличка на волейбол {(dayCount ?? 1) switch { 0 => "СЕГОДНЯ", 1 => "ЗАВТРА", _ => throw new ArgumentException($"{dayCount} не является валидным количеством дней") }}"
                + $" ({date.ToString("dddd", CultureInfo.GetCultureInfo("ru-RU"))}){(string.IsNullOrEmpty(time) ? string.Empty : $" в {time}")}";

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

            bool mustAddOptions = true;
            try
            {
                string strDate = date.ToString(Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd");
                SqliteCommand command = new($"SELECT COUNT(*) FROM seasons_days WHERE \"date\" == \"{strDate}\"", this.GetSqliteConnection());
                SqliteDataReader reader = command.ExecuteReader();
                mustAddOptions = !reader.Read() || reader.GetInt32(0) != 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }

            List<InputPollOption> options = [PLAYER_JOINED, "👀"]; 
            if (mustAddOptions && Environment.GetEnvironmentVariable("SEND_POLL_OPTIONS") is string envOptions && !string.IsNullOrEmpty(envOptions)
				&& envOptions.Split(Commands.PARAMS_DELIMITER) is string[] optionsArray && optionsArray.Length > 0)
			{
				for (int i = 0; i < optionsArray.Length; i++)
					options.Insert(i + 1, optionsArray[i]);
			}

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