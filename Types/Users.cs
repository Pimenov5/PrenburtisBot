﻿using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Types
{
	internal static class Users
	{
		private struct User(long userId, string firstName, double rating)
		{
			public long UserId = userId;
			public string FirstName = firstName;
			public double Rating = rating;
		}

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
				const int FIELD_COUNT = 4;
				if (reader.FieldCount != FIELD_COUNT)
					throw new ArgumentOutOfRangeException(nameof(reader), $"Количество полей в запросе должно быть равно {FIELD_COUNT}");

				_players.Add(new Player(reader.GetInt64(0), reader.GetInt32(2), reader.GetString(1), reader.GetDouble(3)));
				++count;
			}

			return count;
		}

		public static Player GetPlayer(long userId, string firstName, string? username = null)
		{
			Player equalValue = new(userId, default, firstName, default);
			if (!_players.TryGetValue(equalValue, out Player? result))
				return equalValue;

			if (!string.IsNullOrEmpty(username))
				result.Username = username;
			if (firstName != result.FirstName && !string.IsNullOrEmpty(firstName))
			{
				Console.WriteLine($"Имя {result} обновлено на {firstName}");
				result.FirstName = firstName;
			}

			return result;
		}
	}
}