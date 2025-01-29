using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommand("Уведомить игроков из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class NotifyPlayers : RepliedToPollGroupFormBase
	{
		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			StringBuilder stringBuilder = new();
			stringBuilder.AppendJoin(' ', args);
			stringBuilder.AppendLine(Environment.NewLine);
			stringBuilder.AppendJoin(", ", players);	

			return new TextMessage(stringBuilder.ToString()) { ParseMode = ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET); 
		}
	}
}