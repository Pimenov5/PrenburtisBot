using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TL;

namespace PrenburtisBot.Forms
{
	[BotCommand("Добавить к площадке игроков из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class AddPlayers : RepliedToPollGroupFormBase
	{
		private string? _courtId;

		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			_courtId = args.Length >= 1 ? args[0] : null;
			Court court = this.GetCourt(players.Count, userId, out int courtId);

			uint?[] indexes = court.AddPlayers(players);
			int count = 0;
			foreach (uint? index in indexes)
				count = index is null ? count + 1 : count;

			if (count != 0)
				throw new InvalidOperationException($"Не удалось добавить {count} из {indexes.Length} игроков");

			string text = CourtPlayers.ToString(court, userId, this.Device.IsGroup);
			return new TextMessage(text) { ParseMode = ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET);
		}

		protected virtual Court GetCourt(int playersCount, long userId, out int courtId)
		{
			Court court;
			try
			{
				court = Courts.GetById(ref _courtId, userId);
			}
			catch
			{
				if (string.IsNullOrEmpty(_courtId))
					throw new ArgumentNullException(nameof(_courtId), "Вы не указали идентификатор площадки");
				else if (!uint.TryParse(_courtId, out uint value))
					throw new ArgumentOutOfRangeException(nameof(_courtId), $"\"{_courtId}\" не является валидным идентификатором площадки");
				else
					throw new ArgumentOutOfRangeException(nameof(_courtId), $"Не удалось найти площадку с идентификатором {_courtId}");
			}

			int count = 0;
			foreach (Team team in court.Teams)
				count += team.PlayerCount;

			long maxCount = court.Teams.Length * court.TeamMaxPlayerCount;
			if (playersCount > maxCount - count)
				throw new InvalidOperationException($"Недостаточно свободных мест на площадке ({maxCount - count}) для добавления игроков из опроса ({playersCount})");

			courtId = int.Parse(_courtId ?? throw new NullReferenceException());
			return court;
		}
	}
}