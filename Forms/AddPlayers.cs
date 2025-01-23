using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using TelegramBotBase.Form;
using TL;
using Telegram.Bot;

namespace PrenburtisBot.Forms
{
	[BotCommand("Добавить к площадке игроков из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class AddPlayers : BotCommandGroupFormBase
	{
		private string? _courtId;

		public async Task<TextMessage> RenderAsync(MessageResult message)
		{
			if (message.Message.ReplyToMessage is not Telegram.Bot.Types.Message repliedMessage || repliedMessage.Poll is not Telegram.Bot.Types.Poll poll || poll.IsAnonymous
				|| poll.AllowsMultipleAnswers || poll.Options.Length < 1 || poll.Options[0].Text != SendPoll.PLAYER_JOINED)
			{
				return new($"Команда должна вызываться в ответ на не анонимный опрос с первым вариантом ответа \"{SendPoll.PLAYER_JOINED}\"");
			}

			if (TelegramClient is null)
				return new("Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались");

			IReadOnlyCollection<Player> players;
			try
			{
				players = await TelegramClient.GetPlayersFromPoll(repliedMessage, [SendPoll.PLAYER_JOINED_BYTE]);
			}
			catch (Exception e)
			{
				return new(e.Message);
			}

			long userId = message.Message.From?.Id ?? throw new NullReferenceException();
			_courtId = message.BotCommandParameters.Count >= 1 ? message.BotCommandParameters[0] : null;
			Court court = this.GetCourt(players.Count, userId, out int courtId);

			uint?[] teams = court.AddPlayers(players);
			int count = 0;
			foreach (uint? index in teams)
				count = index is null ? count + 1 : count;

			if (count != 0)
				await this.Device.Send($"Не удалось добавить {count} из {teams.Length} игроков");

			string text = CourtPlayers.ToString(court, userId, this.Device.IsGroup);
			return new TextMessage(text) { ParseMode = ParseMode.Markdown, ReplyToMessageId = repliedMessage.MessageId }.NavigateToStart(Start.SET_QUIET);
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

		public static WTelegram.Client? TelegramClient = null;
	}
}