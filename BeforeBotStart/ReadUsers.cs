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
			string dataSource = BeforeBotStartExecutableAttribute.GetPath("PRENBURTIS_DATA_BASE");
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
	}
}