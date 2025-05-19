using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Types
{
	internal static class Users
	{
		private struct User(long userId, string firstName, double rating, bool isActual, Gender gender)
		{
			public long UserId = userId;
			public string FirstName = firstName;
			public double Rating = rating;
			public bool IsActual = isActual;
			public Gender Gender = gender;
		}

		private class PlayerEqualityComparer : EqualityComparer<Player>
		{
			public override bool Equals(Player? x, Player? y) => x == y || x?.UserId == y?.UserId;
			public override int GetHashCode(Player? player) => player is null ? default : player.UserId.GetHashCode();
		}

		private static readonly HashSet<Player> _players = new(new PlayerEqualityComparer());

		public static int Read(SqliteDataReader reader)
		{
			List<User> users = [];
			while (reader.HasRows && reader.Read())
			{
				const int FIELD_COUNT = 5;
				if (reader.FieldCount < FIELD_COUNT)
					throw new ArgumentOutOfRangeException(nameof(reader), $"Количество полей в запросе должно быть не меньше {FIELD_COUNT}");

				long userId = reader.GetInt64(0);
				string firstName = reader.GetString(1);
				char genderChar = reader.GetChar(4);

				users.Add(new(userId, firstName, reader.GetDouble(2), reader.GetBoolean(3), 
					genderChar switch { 'M' => Gender.Male, 'F' => Gender.Female, _ => throw new InvalidCastException($"\"{genderChar}\" не является полом игрока {firstName} ({userId})")}));
			}

			if (users.Count == 0)
				return 0;

			users.Sort((User x, User y) => y.Rating.CompareTo(x.Rating));
			double prevRating = users[0].Rating;
			int rank = 1, count = _players.Count;
			foreach (User user in users) 
			{
				rank = Math.Truncate(user.Rating) != Math.Truncate(prevRating) ? ++rank : rank;
				_players.Add(new(user.UserId, rank, user.FirstName, user.Rating, user.IsActual, user.Gender));
				prevRating = user.Rating;
			}

			return _players.Count - count;
		}

		public static IReadOnlyCollection<Player> GetPlayers() => _players;
		public static Player GetPlayer(long userId, string firstName, string? username = null)
		{
			Player equalValue = new(userId, default, firstName, default, default, default);
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