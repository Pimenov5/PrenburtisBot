using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Args;
using TelegramBotBase.DependencyInjection;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Выбрать тренировки", BotCommandScopeType.AllPrivateChats)]
	internal class JoinSeason : SqliteBotCommandFormBase
	{
		private Season? _season = null;
		private Season Season => _season ?? throw new NullReferenceException("Отсутствует информацию об абонементе");

		private List<DateOnly>? _dates = null;

		private List<DateOnly> GetDatesFromNumbers(string[] args)
		{
			List<DateOnly> result = [];
			int month = this.Season.FirstDate.Month, year = this.Season.FirstDate.Year, prevDay = this.Season.FirstDate.Day;
			foreach (string dayStr in args)
			{
				if (!int.TryParse(dayStr, out int day))
					throw new ArgumentException($"\"{dayStr}\" не является валидным значением дня", nameof(args));

				month = day < prevDay ? month + 1 : month;
				prevDay = day;
				if (month > 12)
				{
					month = 1;
					++year;
				}

				DateOnly dateOnly = default;
				dateOnly = dateOnly.AddDays(day - 1).AddMonths(month - 1).AddYears(year - 1);
				if (dateOnly < this.Season.FirstDate || dateOnly > this.Season.LastDate)
					throw new ArgumentException($"\"{dayStr}\" не удалось преобразовать в дату входящую в пределы абонемента");

				result.Add(dateOnly);
			}

			return result;
		}

		private int WriteDatesToDb(long userId)
		{
			if (_dates is not List<DateOnly> dates)
				throw new NullReferenceException("Отсутствует информация о выбранных датах");

			using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				string commandText = new StringBuilder("INSERT INTO seasons_days (season_id, telegram_id, date) VALUES ")
					.AppendJoin(',', dates.ConvertAll((DateOnly date) => $"({this.Season.Id}, {userId}, \"{date.ToString(Environment.GetEnvironmentVariable("DB_DATE_FORMAT" ?? "yyyy-MM-dd"))}\")")).ToString();
				using SqliteCommand insertCommand = new(commandText, SqliteConnection, transaction);
				using SqliteDataReader insertReader = insertCommand.ExecuteReader();

				if (insertReader.RecordsAffected != dates.Count)
					throw new Exception($"Количество сохранённых дат ({insertReader.RecordsAffected}) не равно количеству выбранных ({dates.Count})");

				transaction.Commit();
				return insertReader.RecordsAffected;
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		private string DatesToString(List<DateOnly> dates)
		{
			StringBuilder stringBuilder = new($"В абонементе (ID {this.Season.Id}) c {this.Season.FirstDate} по {this.Season.LastDate} вы записались в следующие даты ({dates.Count}):" 
				+ Environment.NewLine);
			stringBuilder.AppendJoin(Environment.NewLine, dates);
			return stringBuilder.ToString();
		}

		public async Task<TextMessage?> RenderAsync(long userId)
		{
			if (Users.GetPlayer(userId, string.Empty) is not User user || user.IsArchived)
				return new TextMessage("Только зарегистрированные и активные игроки могут выбирать дни тренировок").NavigateToStart();

			using SqliteCommand seasonCommand = new("SELECT id, first_date, last_date FROM seasons " 
				+ $"WHERE \"{DateTime.UtcNow.ToString((Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd") + " HH:mm:ss")}\" >= opened_timestamp AND closed_timestamp IS NULL AND id = (SELECT MAX(id) FROM seasons)", SqliteConnection);
			using SqliteDataReader seasonReader = seasonCommand.ExecuteReader();
			if (!seasonReader.Read())
				return new TextMessage("Не удалось найти открытый для записи абонемент").NavigateToStart();

			_season = new(seasonReader.GetInt32(0), DateOnly.FromDateTime(seasonReader.GetDateTime(1)), DateOnly.FromDateTime(seasonReader.GetDateTime(2)));
			using SqliteCommand datesCommand = new("SELECT COUNT(\"date\") FROM seasons_days WHERE "
				+ $"seasons_days.telegram_id = {userId} AND seasons_days.season_id = {Season.Id}", SqliteConnection);
			long count = (long)(datesCommand.ExecuteScalar() ?? throw new NullReferenceException("Не удалось выполнить запрос: " + datesCommand.CommandText));

			if (count > 0)
			{
				ConfirmDialog confirmDialog = new($"Количество записанных в абонементе (ID {this.Season.Id}) посещений: {count}." + Environment.NewLine 
					+ "Начать их удаление? Это также необходимо, если вы хотите выбрать другие даты", new("Начать удаление", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
				confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs eventArgs) =>
				{
					await confirmDialog.NavigateTo(bool.Parse(eventArgs.Button.Value) ? new LeaveSeason() : new Start());
				};

				await this.NavigateTo(confirmDialog);
				return null;
			}

			string[]? days = Environment.GetEnvironmentVariable("JOIN_SEASON_DAYS")?.Split(Commands.PARAMS_DELIMITER);
			List<List<string>>? replyMarkup = days is null ? null : [];
			for (int i = 0; days is not null && i < days.Length; i++)
			{
				string button = Season.NumbersToDays(days[i], " ");
				replyMarkup?.Add([button]);
			}

			string text = (replyMarkup is null ? string.Empty : "Выберите один из наборов дней недели ниже или ") + "введите даты через пробел одним из нескольких способов.";
			if (replyMarkup is null)
				text = text[0].ToString().ToUpper() + text[1..];

			text = text + Environment.NewLine + Environment.NewLine
				+ "- Указать только конкретные даты: 1 3 5" + Environment.NewLine
				+ "- Базовые дни недели, исключая даты: all -1 -3 -5" + Environment.NewLine
				+ "- Определённые дни недели, включая и/или исключая даты: Monday/Friday +1 -3 +5";

			text = $"Абонемент (ID {this.Season.Id}) на даты с {this.Season.FirstDate} по {this.Season.LastDate}" + Environment.NewLine + text;
			return new(text) { ReplyMarkup = replyMarkup };
		}

		public async Task<TextMessage?> RenderAsync(long userId, params string[] args)
		{
			_dates = args.All((string value) => int.TryParse(value, out _)) ? this.GetDatesFromNumbers(args) : this.Season.ParseDates(args);
			if (_dates.Count == 0)
				return new("Количество выбранных дней не может быть равно 0");
			if (_dates.Any((DateOnly date) => _dates.Count((DateOnly item) => item == date) > 1))
				return new("Вы ввели повторяющиеся даты");

			await this.Device.Send(this.DatesToString(_dates));

			ConfirmDialog confirmDialog = new("Сохранить выбранные дни тренировок?", new("Сохранить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs eventArgs) =>
			{
				await confirmDialog.NavigateTo(this, eventArgs.Button.Value);
			};

			await this.NavigateTo(confirmDialog);
			return null;
		}

		public async Task<TextMessage?> RenderAsync(string strIsConfirmed)
		{
			if (!bool.TryParse(strIsConfirmed, out bool isConfirmed))
				return await RenderAsync(this.Device.DeviceId, [strIsConfirmed]);
			else  if (!isConfirmed)
				return new TextMessage(string.Empty).NavigateToStart();

			int count = this.WriteDatesToDb(this.Device.DeviceId);
			return new TextMessage($"Количество сохранённых дат: {count}").NavigateToStart();
		}
	}
}