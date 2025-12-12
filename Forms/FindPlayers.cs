using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommand("Найти недостающих игроков", BotCommandScopeType.AllChatAdministrators)]
	internal class FindPlayers : RepliedToPollGroupFormBase
	{
		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			List<Player> newPlayers = [..Users.GetPlayers()];
			newPlayers.RemoveAll((Player player) => player is not User user || user.IsArchived || players.Any((player) => player.UserId == user.UserId));
			if (newPlayers.Count == 0)
				throw new ArgumentException($"Все не архивные игроки уже проголосовали в \"{SendPoll.PLAYER_JOINED}\"", nameof(players));

			int needCount = (players.Count < 12 ? 12 : 14) - players.Count;
			const string FIND_PLAYERS_MESSAGE = "Сегодня не хватает {0} игрок{1}. Будем рады желающим, чтобы играть полными командами!";
			StringBuilder stringBuilder = (args.Length == 0 ? new(string.Format(FIND_PLAYERS_MESSAGE, needCount, needCount switch { 1 => "а", _ => "ов" })) 
				: new StringBuilder()).AppendJoin(" ", args).AppendLine(Environment.NewLine).AppendJoin(", ", newPlayers);

			return new(stringBuilder.ToString()) { ParseMode = ParseMode.Markdown };
		}
	}
}