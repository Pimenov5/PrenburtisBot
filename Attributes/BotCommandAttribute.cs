using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Attributes
{
	[AttributeUsage(AttributeTargets.Class)]
	internal class BotCommandAttribute(string description, BotCommandScopeType scope = BotCommandScopeType.AllPrivateChats) : Attribute
	{
		public readonly string Description = description;
		public readonly BotCommandScopeType Scope = scope;
	}
}