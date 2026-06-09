using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Открыть форму голосования", "BOT_OWNER_CHAT_ID")]
	internal class OpenVoteRatings : SqliteBotCommandFormBase
	{
		private DateTime? _openedDateTime = null;
		private readonly List<long> _playersIds = [];
		private readonly Dictionary<int, List<Player>> _players = [];
		private const string NEED_MIN_ATTENDANCE = "Введите минимальное количество посещений для участия в голосовании";

		private static string GetSortedPlayersString(Dictionary<int, List<Player>> players, IList<long>? idsList = null)
		{
			List<KeyValuePair<int, List<Player>>> sortedPlayers = [.. idsList is null ? players : players.Where((pair) => pair.Value.Any((Player player) => idsList.Contains(player.UserId)))];
			sortedPlayers.Sort((x, y) => y.Key.CompareTo(x.Key));
			StringBuilder stringBuilder = new();
			foreach (KeyValuePair<int, List<Player>> pair in sortedPlayers)
				stringBuilder.AppendLine(new StringBuilder($"{pair.Key}: ").AppendJoin(", ", pair.Value).ToString());

			return stringBuilder.ToString();
		}

		public TextMessage Render()
		{
			using SqliteCommand closedFormsCommand = new("SELECT id FROM ratings_forms WHERE closed_timestamp IS NULL", SqliteConnection);
			using SqliteDataReader closedFormsReader = closedFormsCommand.ExecuteReader();
			List<int> notClosedForms = [];
			while (closedFormsReader.Read())
				notClosedForms.Add(closedFormsReader.GetInt32(0));
			
			if (notClosedForms.Count > 0)
				throw new InvalidOperationException("Невозможно открыть новую форму голосования пока не закрыты с идентификаторами: " + new StringBuilder().AppendJoin(", ", notClosedForms).ToString());
			
			using SqliteCommand prevFormCommand = new("SELECT MAX(id), closed_timestamp FROM ratings_forms WHERE closed_timestamp IS NOT NULL", SqliteConnection);
			using SqliteDataReader prevCommandReader = prevFormCommand.ExecuteReader();
			prevCommandReader.Read();
			long prevFormId = prevCommandReader.GetInt64(0);
			string prevFormClosedString = prevCommandReader.GetString(1);

			using SqliteCommand attendanceCommand = new("SELECT users.telegram_id, (SELECT COUNT(attendance_users.telegram_id) FROM attendance_users, attendance "
				+ "WHERE attendance_users.telegram_id = users.telegram_id AND attendance_users.attendance_id = attendance.id AND attendance.timestamp > \"" + prevFormClosedString
				+ "\") as count FROM users WHERE count >= 1 --ORDER BY count DESC", SqliteConnection);
			using SqliteDataReader attendanceReader = attendanceCommand.ExecuteReader();

			int count = 0;
			while (attendanceReader.Read())
			{
				int key = attendanceReader.GetInt32(1);
				Player value = Users.GetPlayer(attendanceReader.GetInt64(0), string.Empty);
				if (!_players.ContainsKey(key))
					_players.Add(key, []);

				_players[key].Add(value);
				count++;
			}

			return new($"Количество посещений игроками ({count}) с {prevFormClosedString} ({prevFormId})" + Environment.NewLine
				+ GetSortedPlayersString(_players) + Environment.NewLine + NEED_MIN_ATTENDANCE) { ParseMode = ParseMode.Markdown };
		}

		public async Task<TextMessage> RenderAsync(params string[] args)
		{
			int minAttendance = int.MinValue;
			const int MIN_PLAYERS_COUNT = VoteRatings.MAX_RATING + 1;

			if (_playersIds.Count < MIN_PLAYERS_COUNT && args.Length == 1 && int.TryParse(args[0], out minAttendance) && minAttendance > 1)
			{
				foreach (KeyValuePair<int, List<Player>> pair in _players)
					if (pair.Key >= minAttendance)
						_playersIds.AddRange(pair.Value.ConvertAll<long>((Player player) => player.UserId));
			}

			if (_playersIds.Count < MIN_PLAYERS_COUNT)
			{
				int count = _playersIds.Count;
				_playersIds.Clear();
				return new((count > 0 ? $"Количество игроков ({count}) с доступом к форме голосования не может быть меньше {MIN_PLAYERS_COUNT}."
					: minAttendance < 1 && args.Length == 1 ? "Минимальное количество посещений не может быть меньше 1." : string.Empty) + Environment.NewLine + NEED_MIN_ATTENDANCE);
			}

			if (_openedDateTime is null && args.Length == 2
				&& DateTime.TryParse(DateOnly.TryParse(args[0], out DateOnly dateOnly) ? $"{args[0]} {args[1]}" : $"{args[1]} {args[0]}", out DateTime openDateTime))
			{
				_openedDateTime = openDateTime;
			}
			
			if (_openedDateTime is null)
				return new("Введите UTC дату и время открытия формы голосования") { ReplyMarkup = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm") };			

			if (args.Length == 2)
			{
				ConfirmDialog confirmDialog = new($"Создать форму голосования открытую с {_openedDateTime}? Количество игроков с доступом = {_playersIds.Count}",
					new("Продолжить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
				confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs eventArgs) => await confirmDialog.NavigateTo(this, eventArgs.Button.Value);

				await this.NavigateTo(confirmDialog);
				return new(string.Empty);
			}
			else if (args.Length == 1 && bool.TryParse(args[0], out bool isConfirmed) && !isConfirmed)
				return new TextMessage("Отменено создание формы голосования для обновления рейтинга игроков").NavigateToStart();

			long? formId = null;
			using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				string format = (Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd") + " HH:mm:ss";
				using SqliteCommand insertFormCommand = new("INSERT INTO ratings_forms (opened_timestamp) VALUES (\"" + _openedDateTime?.ToString(format) + "\") RETURNING id", SqliteConnection, transaction);
				formId = (long)(insertFormCommand.ExecuteScalar() ?? throw new NullReferenceException("Не удалось выполнить запрос: " + insertFormCommand.CommandText));

				using SqliteCommand insertPermissionsCommand = new(new StringBuilder("INSERT INTO ratings_forms_permissions (ratings_form_id, telegram_id) VALUES ").AppendJoin(',',
					_playersIds.ConvertAll<string>((long userId) => $"({formId},{userId})")).ToString(), SqliteConnection, transaction);
				using SqliteDataReader insertPermissionsReader = insertPermissionsCommand.ExecuteReader();
				if (insertPermissionsReader.RecordsAffected != _playersIds.Count)
					throw new Exception("Не удалось выполнить запрос: " + insertPermissionsCommand.CommandText);

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
			
			using SqliteCommand selectFormCommand = new("SELECT MAX(id), opened_timestamp FROM ratings_forms WHERE closed_timestamp IS NULL", SqliteConnection);
			using SqliteDataReader selectFormReader = selectFormCommand.ExecuteReader();
			selectFormReader.Read();
			long maxFormId = selectFormReader.GetInt64(0);
			DateTime maxOpenedDateTime = selectFormReader.GetDateTime(1);

			if (maxFormId != formId)
				throw new Exception($"Идентификатор последней формы голосования ({maxFormId}) не соответствует добавленному ({formId})");

			StringBuilder stringBuilder = new();
			stringBuilder.AppendLine((DateTime.UtcNow > maxOpenedDateTime ? "Открыта" : DateTime.UtcNow.Date.Equals(maxOpenedDateTime.Date)
				? $"Сегодня в {maxOpenedDateTime.TimeOfDay} будет открыта" : maxOpenedDateTime.ToString() + " будет открыта")
				+ $" форма голосования (ID {formId}) для обновления рейтинга игроков. Отправлять ответы могут следующие игроки ({_playersIds.Count}) по количеству посещений." + Environment.NewLine);
			stringBuilder.AppendLine(GetSortedPlayersString(_players, _playersIds));
			stringBuilder.AppendLine("Минимальное количество ответов для принятия результатов = " + Math.Ceiling(_playersIds.Count / 2.0).ToString() 
				+ ". Если вы не хотите или не можете принять участие в голосовании, пожалуйста, напишите об этом." + Environment.NewLine);
			if (Environment.GetEnvironmentVariable("TIER_MAKER_LINK") is string tierMakerLink)
				stringBuilder.AppendLine("Рекомендуется использовать визуальный тир-лист для упрощения распределения игроков между оценками: " + tierMakerLink + Environment.NewLine);
			stringBuilder.AppendLine("Заполнить форму голосования: t.me/" + (await this.API.GetMe()).Username + $"?{nameof(Start).ToLower()}=" + nameof(VoteRatings).ToLower());

			return new TextMessage(stringBuilder.ToString()) { ParseMode = ParseMode.Markdown, ReplyMarkup = ReplyMarkup.RemoveKeyboard, LinkPreviewOptions = true }.NavigateToStart();
		}
	}
}