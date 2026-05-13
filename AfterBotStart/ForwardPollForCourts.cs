using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PrenburtisBot.AfterBotStart
{
	[NeededTelegramClient]
	internal class ForwardPollForCourts : IAfterBotStartAsyncExecutable
	{
		private static WTelegram.Client? _telegramClient = null;

		public static WTelegram.Client TelegramClient { set { _telegramClient = value; } }

		public async Task<TextMessage> ExecuteAsync(ITelegramBotClient botClient, ChatId chatId, int? messageThreadId, params string[] args)
		{
			if (_telegramClient is null)
				throw new NullReferenceException("Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались");

			string pollMessageId = Session.Get(typeof(Forms.SendPoll), chatId.ToString()) ?? throw new NullReferenceException("Отсутствует идентификатор сообщения с опросом");
			int messageId = int.TryParse(pollMessageId, out int intValue) ? intValue : throw new InvalidCastException($"{pollMessageId} не является валидным идентификатором сообщения");
			Message message = await botClient.ForwardMessage(chatId, chatId, messageId, messageThreadId);

			string text = $"/{typeof(Forms.CourtsForPoll).Name.ToLower()} {Forms.AddPlayers.MUST_REMOVE_POLL} @{(await botClient.GetMe()).Username}";
			TL.InputChannel inputChannel = await _telegramClient.GetInputChannelAsync(message.Chat);
			TL.Message userMessage = await _telegramClient.SendMessageAsync(inputChannel, text, reply_to_msg_id: message.Id);

			return new($"\"{userMessage.message}\" ({userMessage.id}) отправлено в ответ на сообщение {(message.Poll is null ? $"\"{message.Text}\"" : "с опросом")} ({message.Id})");
		}
	}
}