using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot.Types;
using TelegramBotBase;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Открыть месячный абонемент", "BOT_OWNER_CHAT_ID")]
	internal class OpenSeason : SqliteBotCommandFormBase
	{
		public const string DAYS_SEPARATOR = ", ";

		public async Task<TextMessage> RenderAsync(params string[] args)
		{
			if (args.Length != 2 || !DateOnly.TryParse(args[0], out DateOnly firstDate) || !DateOnly.TryParse(args[1], out DateOnly lastDate) || firstDate.Month != lastDate.Month
				|| firstDate.Year != lastDate.Year || firstDate > lastDate)
			{
				int year = DateTime.Now.Year;
				return new("Введите через пробел два параметра: даты первого и последнего дня абонемента в одном месяце, например," + Environment.NewLine + $"01.01.{year} 31.01.{year}");
			}
			
			long seasonId;
			SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				using SqliteCommand selectCommand = new("SELECT id FROM seasons WHERE closed_timestamp IS NULL", SqliteConnection, transaction);
				using SqliteDataReader selectReader = selectCommand.ExecuteReader();
				if (selectReader.Read())
				{
					seasonId = selectReader.GetInt32(0);
					throw new($"Невозможно открыть новый абонемент пока открыт с ID {seasonId}");
				}

				string format = Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd";
				using SqliteCommand insertCommand = new($"INSERT INTO seasons (first_date, last_date) VALUES (\"{firstDate.ToString(format)}\", \"{lastDate.ToString(format)}\") "
					+ "RETURNING id", SqliteConnection, transaction);
				seasonId = (long)(insertCommand.ExecuteScalar() ?? throw new NullReferenceException("Не удалось открыть новый абонемент"));
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
			
			if (Environment.GetEnvironmentVariable("OPEN_SEASON_POLL_OPTIONS") is not string envOptions)
				return new TextMessage($"Открыт абонемент (ID {69}) с {firstDate} по {lastDate}").NavigateToStart();
			
			string[] strArray = envOptions.Split(Commands.PARAMS_DELIMITER);
			List<InputPollOption> options = new(strArray.Length);
			foreach (string item in strArray)
			{
				string option = int.TryParse(item, out _) ? Season.NumbersToDays(item, DAYS_SEPARATOR) : item;
				options.Add(new(option));
			}
			
			await this.Device.SendPoll($"Выберите дни посещений для абонемента с {firstDate.ToString("dd.MM")} по {lastDate.ToString("dd.MM")}", options, false, PollType.Regular, false);

			return new TextMessage("Выбрать даты через бота: " + await Start.GetDeepLinkAsync(this.API, typeof(JoinSeason))).NavigateToStart();
		}
	}
}