using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Attributes
{
	internal class BotCommandChatAttribute(string description, ChatId chatId, BotCommandScopeType scope = BotCommandScopeType.Chat) : BotCommandAttribute(description, scope)
	{
		public readonly ChatId ChatId = chatId;
	}
}