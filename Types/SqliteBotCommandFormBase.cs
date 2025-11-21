using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Types
{
	internal class SqliteBotCommandFormBase : BotCommandFormBase
	{
		private static SqliteConnection? s_connection;

		protected static SqliteConnection SqliteConnection { 
			get
			{
				if (s_connection is null)
				{
					SqliteConnectionStringBuilder builder = new() { Mode = SqliteOpenMode.ReadWrite };
					s_connection = new(builder.SetDataSource("PRENBURTIS_DATA_BASE").ConnectionString);
					s_connection.Open();
				}

				return s_connection;
			} 
		}
	}
}