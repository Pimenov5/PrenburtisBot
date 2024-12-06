using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Attributes
{
	internal class BotCommandChatAttribute(string description, string? chatId, BotCommandScopeType scope = BotCommandScopeType.Chat) : BotCommandAttribute(description, scope)
	{
		public readonly string? ChatId = chatId;
	}
}