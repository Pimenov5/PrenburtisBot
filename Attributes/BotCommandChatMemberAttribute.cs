using Telegram.Bot.Types;

namespace PrenburtisBot.Attributes
{
	internal class BotCommandChatMemberAttribute(string description, string? chatId, long userId) : BotCommandChatAttribute(description, chatId)
	{
		public readonly long UserId = userId;
	}
}