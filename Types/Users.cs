using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Types
{
	internal static class Users
	{
		private class UserEqualityComparer : EqualityComparer<User>
		{
			public override bool Equals(User? x, User? y) => x == y || x?.UserId == y?.UserId;
			public override int GetHashCode(User? player) => player is null ? default : player.UserId.GetHashCode();
		}

		private static readonly HashSet<User> _users = new(new UserEqualityComparer());

		public static int Read(SqliteDataReader reader)
		{
			int count = _users.Count;
			while (reader.HasRows && reader.Read())
			{
				const int FIELD_COUNT = 6;
				if (reader.FieldCount < FIELD_COUNT)
					throw new ArgumentOutOfRangeException(nameof(reader), $"Количество полей в запросе должно быть не меньше {FIELD_COUNT}");

				long userId = reader.GetInt64(0);
				string firstName = reader.GetString(1);
				char genderChar = reader.GetChar(3);
				Gender gender = genderChar switch { 'M' => Gender.Male, 'F' => Gender.Female,
					_ => throw new InvalidCastException($"\"{genderChar}\" не является полом игрока {firstName} ({userId})") };
				Skills skills = new(reader.IsDBNull(6) ? 1.0 : reader.GetDouble(6), reader.IsDBNull(7) ? 1.0 : reader.GetDouble(7), reader.IsDBNull(8) ? 1.0 : reader.GetDouble(8));

				_users.Add(new(userId, firstName, reader.IsDBNull(2) ? default : reader.GetDouble(2), gender, skills, reader.GetBoolean(4), reader.GetBoolean(5)));
			}

			return _users.Count - count;
		}

		public static IReadOnlyCollection<Player> GetPlayers() => _users;
		public static Player GetPlayer(long userId, string firstName, string? username = null)
		{
			User equalValue = new(userId, firstName, default, default, default, default, default);
			if (!_users.TryGetValue(equalValue, out User? result))
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