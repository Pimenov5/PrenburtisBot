using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Extensions;
using PrenburtisBot.Types;

namespace PrenburtisBot.BeforeBotStart
{
	[BeforeBotStartExecutable(nameof(ReadUsers.FromSQLiteDb))]
	internal static class ReadUsers
	{
		private static string GetCommandText()
		{
			const string USERS_COMMAND_TEXT = "USERS_COMMAND_TEXT";
			if (Environment.GetEnvironmentVariable(USERS_COMMAND_TEXT) is not string commandText)
				throw new EnvVariableException(USERS_COMMAND_TEXT);
			return commandText;
		}

		public static string FromSQLiteDb()
		{
			SqliteConnection connection = FormBaseExtensions.GetSqliteConnection();
			Console.WriteLine($"Установлено соединение с {connection.DataSource}");

			string commandText = GetCommandText();
			using SqliteCommand command = new(commandText, connection);
			using SqliteDataReader reader = command.ExecuteReader();
			return $"Добавлены ранговые игроки ({Users.Read(reader)})";
		}

		public static int UpdateFromSqliteDb(IReadOnlyCollection<long>? ids = null)
		{
			string commandText = GetCommandText();
			commandText = commandText.Replace("*", "telegram_id, user_rating, passing, setting, attacking");
			if (ids is not null)
				commandText = commandText + " WHERE telegram_id IN (" + String.Join(',', ids) + ")";

			SqliteConnection connection = FormBaseExtensions.GetSqliteConnection();
			using SqliteCommand command = new(commandText, connection);
			using SqliteDataReader reader = command.ExecuteReader();
			if (!Users.TryUpdateRatingsAndSkills(reader, ids is null ? null : (int count) => count == ids.Count,  out int count))
				throw new("Не удалось обновить рейтинги игроков");

			Console.WriteLine($"Обновлены рейтинги и навыки игроков ({count})");
			return count;
		}
	}
}