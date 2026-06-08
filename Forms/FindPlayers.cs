using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommand("Найти недостающих игроков", BotCommandScopeType.AllChatAdministrators)]
	internal class FindPlayers : NotifyNotVoted
	{
		protected override string? GetFirstPollOption() => SendPoll.PLAYER_JOINED;

		protected override string GetText(IReadOnlyCollection<Player> players, List<Player> notVoted, params string[] args)
		{
			int needCount = (players.Count < 12 ? 12 : 14) - players.Count;
			const string FIND_PLAYERS_MESSAGE = "Сегодня не хватает {0} игрок{1}. Будем рады желающим, чтобы играть полными командами!";
			StringBuilder stringBuilder = (args.Length == 0 ? new(string.Format(FIND_PLAYERS_MESSAGE, needCount, needCount switch { 1 => "а", _ => "ов" }))
				: new StringBuilder()).AppendJoin(" ", args).AppendLine(Environment.NewLine).AppendJoin(", ", notVoted);

			return stringBuilder.ToString();
		}
	}
}