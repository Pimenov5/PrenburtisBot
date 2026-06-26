using Microsoft.Data.Sqlite;
using PrenburtisBot.Types;

namespace PrenburtisBot.Extensions
{
	internal static class SqliteConnectionStringBuilderExtensions
	{
		public static SqliteConnectionStringBuilder SetDataSource(this SqliteConnectionStringBuilder connectionStringBuilder, string variable)
		{
			if (Environment.GetEnvironmentVariable(variable) is not string path || string.IsNullOrEmpty(path))
				throw new EnvVariableException(variable);

			if (!File.Exists(path))
				throw new FileNotFoundException($"Отсутствует файл БД по пути: {path}");

			connectionStringBuilder.DataSource = path;
			return connectionStringBuilder;
		}
	}
}