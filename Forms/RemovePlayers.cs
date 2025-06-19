﻿using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;

namespace PrenburtisBot.Forms
{
	[BotCommand("Удалить игроков из всех команд на площадке")]
	internal class RemovePlayers : BotCommandFormBase
	{
		private string? _courtId;

		public TextMessage Render(long userId, params string[] args)
		{
			_courtId ??= args.Length > 0 && int.TryParse(args[0], out int value) ? args[0] : null;
			Court court = Courts.GetById(ref _courtId, userId);
			if (court.UserId != userId)
				return new("Удалять игроков из команд на площадке может только её создатель");

			if (court.Teams.All((Team team) => team.PlayerCount == 0))
				return new TextMessage(string.Empty) { NavigateTo = new(new CourtPlayers(), _courtId ?? throw new NullReferenceException(nameof(_courtId))) };

			args = args.Length > 0 && args[0] == _courtId ? args[1..] : args;
			if (args.Length == 0)
				return new("Введите имена или юзернеймы игроков через пробел");
			else if (args.Length == 1 && Environment.GetEnvironmentVariable("REMOVE_ALL_PLAYERS_ALIAS") is string alias && !string.IsNullOrEmpty(alias) && args[0] == alias)
			{
				foreach (Team team in court.Teams)
					team.RemovePlayers(0, team.PlayerCount);

				return new TextMessage(string.Empty) { NavigateTo = new(new CourtPlayers(), _courtId ?? throw new NullReferenceException(nameof(_courtId))) };
			}

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

					if (names.Count != count)
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
					1 => $"удален(а) из команды #{++item.Value[0]}",
					2 => $"удален(а) из команд #{++item.Value[0]} и #{++item.Value[1]}",
					_ => "удален(а) из команд под номерами: " + new StringBuilder().AppendJoin(", ", item.Value.ToList().ConvertAll((int index) => 1 + index)).ToString()
				}}");

			TextMessage textMessage = new(stringBuilder.ToString()) { ParseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown };
			return names.Values.Any((int[] indexes) => indexes.Length == 0) ? textMessage : textMessage.NavigateToStart(Start.SET_QUIET);
		}
	}
}