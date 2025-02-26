using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.BeforeBotStart
{
	[BeforeBotStartExecutable(nameof(ReadUsers.FromSQLiteDb))]
	internal static class ReadUsers
	{
		public static string FromSQLiteDb()
		{
			string dataSource = BeforeBotStartExecutableAttribute.GetPath("PRENBURTIS_DATA_BASE");
			const string USERS_COMMAND_TEXT = "USERS_COMMAND_TEXT";
			if (Environment.GetEnvironmentVariable(USERS_COMMAND_TEXT) is not string commandText)
				throw new Exception($"Отсутствует значение переменной окружения {USERS_COMMAND_TEXT}");

			SqliteConnectionStringBuilder connectionStringBuilder = new() { Mode = SqliteOpenMode.ReadOnly, DataSource = dataSource };
			using SqliteConnection connection = new(connectionStringBuilder.ConnectionString);
			connection.Open();
			Console.WriteLine($"Установлено соединение с {connection.DataSource}");

			using SqliteCommand command = new(commandText, connection);
			using SqliteDataReader reader = command.ExecuteReader();
			return $"Добавлены ранговые игроки ({Users.Read(reader)})";
		}
	}
}