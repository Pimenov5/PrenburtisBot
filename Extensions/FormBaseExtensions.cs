using Microsoft.Data.Sqlite;
using PrenburtisBot.Types;
using System.Data.Common;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Extensions
{
	internal static class FormBaseExtensions
	{
		private static SqliteConnection? s_connection = null;

		public static string[] GetBotCommandParameters(this FormBase formBase, MessageResult messageResult)
		{
			List<string> botCommandParameters = messageResult.BotCommandParameters.Count > 0 ? messageResult.BotCommandParameters : [..(!string.IsNullOrEmpty(messageResult.BotCommand)
				&& messageResult.Command.Contains(messageResult.BotCommand) ? messageResult.Command.Replace(messageResult.BotCommand, string.Empty) : messageResult.Command).Split(' ')];
			botCommandParameters.RemoveAll((string value) => string.IsNullOrEmpty(value));

			if (botCommandParameters.Count > 0 && botCommandParameters[0].Equals(formBase.GetType().Name, StringComparison.OrdinalIgnoreCase))
				botCommandParameters.RemoveAt(0);

			return [..botCommandParameters];
		}

		public static SqliteConnection GetSqliteConnection(this FormBase? formBase)
		{
			if (s_connection is null)
			{
				const string DATA_SOURCE = "PRENBURTIS_DATA_BASE";
				if (Environment.GetEnvironmentVariable(DATA_SOURCE) is not string path || string.IsNullOrEmpty(path))
					throw new EnvVariableException(DATA_SOURCE);

				if (!File.Exists(path))
					throw new FileNotFoundException($"Отсутствует файл БД по пути: {path}");

				SqliteConnectionStringBuilder builder = new() { DataSource = path, Mode = SqliteOpenMode.ReadWrite };
				s_connection = new(builder.ConnectionString);
				s_connection.Open();
			}

			return s_connection;
		}

		public static SqliteConnection GetSqliteConnection() => GetSqliteConnection(null);
	}
}