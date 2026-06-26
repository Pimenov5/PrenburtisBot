using Microsoft.Data.Sqlite;
using PrenburtisBot.Extensions;

namespace PrenburtisBot.Types
{
	internal class SqliteBotCommandFormBase : BotCommandFormBase
	{
		protected static SqliteConnection SqliteConnection => FormBaseExtensions.GetSqliteConnection();
	}
}