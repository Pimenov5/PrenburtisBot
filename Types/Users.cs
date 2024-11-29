using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Types
{
	internal static class Users
	{
		private class PlayerEqualityComparer : EqualityComparer<Player>
		{
			public override bool Equals(Player? x, Player? y) => x == y || x?.UserId == y?.UserId;
			public override int GetHashCode(Player? player) => player is null ? default : player.UserId.GetHashCode();
		}

		private static readonly HashSet<Player> _players = new(new PlayerEqualityComparer());

		public static int Read(SqliteDataReader reader)
		{
			int count = 0;
			while (reader.HasRows && reader.Read())
			{
				const int FIELD_COUNT = 3;
				if (reader.FieldCount != FIELD_COUNT)
					throw new ArgumentOutOfRangeException(nameof(reader), $"Количество полей в запросе должно быть равно {FIELD_COUNT}");

				_players.Add(new Player(reader.GetInt64(0), reader.GetInt32(2), reader.GetString(1)));
				++count;
			}

			return count;
		}

		public static Player GetPlayer(long userId, string firstName)
		{
			Player player = new(userId, default, firstName);
			return _players.TryGetValue(player, out Player? result) ? result : player;
		}
	}
}