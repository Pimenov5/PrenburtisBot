using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotBase.Base;
using System.Text;

namespace PrenburtisBot.Forms
{
	[BotCommand("Остановить опрос", Telegram.Bot.Types.Enums.BotCommandScopeType.AllChatAdministrators)]
	internal class StopPoll : BotCommandGroupFormBase, IAfterBotStartAsyncExecutable
	{
		public async Task<TextMessage> ExecuteAsync(ITelegramBotClient botClient, ChatId chatId, int? messageThreadId, params string[] args)
		{
			await botClient.StopPoll(chatId, args.Length == 1 && int.TryParse(args[0], out int messageId) ? messageId 
				: throw new ArgumentException("Некорректное значение идентификатора сообщения с опросом: " + new StringBuilder().AppendJoin(", ", args).ToString(), nameof(args)));
			return new TextMessage(string.Empty);
		}

		public async Task<TextMessage> RenderAsync(long chatId, int messageId)
		{
			return await ExecuteAsync(this.API, chatId, null, [messageId.ToString()]);
		}

		public async Task<TextMessage> RenderAsync(MessageResult message)
		{
			if (message.Message.ReplyToMessage is Message replyToMessage && replyToMessage.Poll is Poll poll)
				return poll.IsClosed ? new TextMessage(string.Empty).NavigateToStart(Start.SET_QUIET) : await RenderAsync(replyToMessage.Chat.Id, replyToMessage.MessageId);
			else
				throw new ArgumentException("Команда должна вызываться в ответ на опрос", nameof(message));
		}
	}
}