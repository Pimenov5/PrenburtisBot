using Telegram.Bot;
using Telegram.Bot.Types;

namespace PrenburtisBot.Types
{
	internal interface IAfterBotStartAsyncExecutable
	{
		public Task<TextMessage> ExecuteAsync(ITelegramBotClient botClient, ChatId chatId, int? messageThreadId, params string[] args);
	}
}