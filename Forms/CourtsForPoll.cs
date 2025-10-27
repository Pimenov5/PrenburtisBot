using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase;
using TelegramBotBase.Base;

namespace PrenburtisBot.Forms
{
	[BotCommand("Создать площадки для опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class CourtsForPoll : CreateCourt
	{
		private class SimpleCourt
		{
			private readonly List<SortedSet<long>> _teams;

			public int TeamCount => _teams.Count;
			public IEnumerable<SortedSet<long>> Teams => _teams;

			public SimpleCourt(Court court)
			{
				_teams = new(court.TeamCount);
				foreach (Team team in court.Teams)
				{
					SortedSet<long> newTeam = [];
					foreach (Player player in team.Players)
						newTeam.Add(player.UserId);

					_teams.Add(newTeam);
				}
			}

			public bool Equals(SimpleCourt court)
			{
				if (this.TeamCount != court.TeamCount)
					return false;

				bool result = false;
				foreach (SortedSet<long> team in court.Teams)
				{
					result = false;
					foreach (SortedSet<long> thisTeam in _teams)
						if (thisTeam.SetEquals(team))
						{
							result = true;
							break;
						}

					if (!result)
						break;
				}

				return result;
			}
		}

		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, MessageResult message, params string[] args)
		{
			Court newCourt;
			string? value = Environment.GetEnvironmentVariable("TEAMS_NAMES_WRITE_SESSION");
			try
			{
				newCourt = this.GetCourt(players.Count, userId, out int courtId);
			}
			finally
			{
				Environment.SetEnvironmentVariable("TEAMS_NAMES_WRITE_SESSION", value);
			}

			if (newCourt is not RankedCourt)
			{
				if (Courts.IndexOf(newCourt) is int index && index >= 0)
					Courts.RemoveAt(index);

				throw new InvalidOperationException("Невозможно применить настройки распределения по командам, т.к. созданная из опроса площадка не является рейтинговой");
			}

			RankedCourt court = (RankedCourt)newCourt;
			if (court.AddPlayers(players).Length is int length && length != players.Count)
				throw new ArgumentException($"Не удалось добавить всех игроков ({players.Count - length}) к площадке", nameof(players));

			foreach (Team team in court.Teams)
				team.Name = string.Empty;

			int i = 0;
			List<SimpleCourt> courts = new(3);
			List<InputPollOption> options = new(3);
			int? messageThreadId = message.Message.Chat.IsForum ? message.Message.MessageThreadId : null;

			while (courts.Count < 3)
			{
				court.Settings = i++ switch { 0 => new(true, false), 1 => new(true, true), _ => new(false, true, true, 2) };
				if (!court.Shuffle())
					throw new InvalidOperationException("Не удалось перемешать игроков на площадке");

				SimpleCourt? simpleCourt = new(court);
				foreach (SimpleCourt item in courts)
					if (item.Equals(simpleCourt))
					{
						simpleCourt = null;
						break;
					}

				if (simpleCourt is null)
					continue;
				else 
					courts.Add(simpleCourt);

				Task messageTask = this.API.SendMessage(this.Device.DeviceId, CourtPlayers.ToString(court, userId, this.Device.IsGroup), ParseMode.Markdown, messageThreadId: messageThreadId);
				options.Add($"Вариант #{options.Count + 1}");
				messageTask.Wait();
			}

			Task pollTask = this.API.SendPoll(this.Device.DeviceId, "Каким составом команд играем сегодня?", options, allowsMultipleAnswers: true, messageThreadId: messageThreadId);
			pollTask.Wait();

			return new TextMessage(string.Empty).NavigateToStart(Start.SET_QUIET);
		}
	}
}