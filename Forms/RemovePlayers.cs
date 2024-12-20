using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;

namespace PrenburtisBot.Forms
{
	[BotCommand("Удалить игроков из всех команд на площадке")]
	internal class RemovePlayers : BotCommandFormBase
	{
		private string? _courtId;

		protected override async Task<TextMessage?> RenderAsync(long userId, params string[] args)
		{
			_courtId ??= args.Length > 0 ? args[0] : null;
			Court court = Courts.GetById(ref _courtId, userId);
			if (court.UserId != userId)
				return new("Удалять игроков из команд на площадке может только её создатель");

			args = args.Length > 0 && args[0] == _courtId ? args[1..] : args;
			if (args.Length == 0)
				return new("Введите имена или юзернеймы игроков через пробел");

			Dictionary<string, int[]> names = new(args.Length);
			foreach (string name in args)
			{
				int count = names.Count;
				Func<Player, string?> getName = name.StartsWith('@') ? (player) => '@' + player.Username : (player) => player.FirstName;
				foreach (Team team in court.Teams)
				{
					foreach (Player player in team.Players)
						if (name == getName(player))
						{
							names.Add(player.Link, court.RemovePlayer(player.UserId, false));
							break;
						}

					if (names.Count == count)
						break;
				}

				if (names.Count == count)
					names.Add(name, []);
			}

			StringBuilder stringBuilder = new();
			foreach (KeyValuePair<string, int[]> item in names)
				stringBuilder.AppendLine($"{item.Key} {item.Value.Length switch
				{
					0 => "не удалось найти в командах",
					1 => $"удален(а) из команды #{item.Value[0]}",
					2 => $"удален(а) из команд #{item.Value[0]} и #{item.Value[1]}",
					_ => "удален(а) из команд под номерами: " + new StringBuilder().AppendJoin(", ", item.Value.ToList().ConvertAll((int index) => 1 + index)).ToString()
				}}");

			return new TextMessage(stringBuilder.ToString()) { ParseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET);
		}
	}
}