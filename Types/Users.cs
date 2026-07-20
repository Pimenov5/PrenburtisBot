using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace PrenburtisBot.Types
{
	internal static class Users
	{
		private class UserEqualityComparer : EqualityComparer<User>
		{
			public override bool Equals(User? x, User? y) => x == y || x?.UserId == y?.UserId;
			public override int GetHashCode(User? player) => player is null ? default : player.UserId.GetHashCode();
		}

		private class TempUser(long userId, string firstName, double rating, Gender gender, Skills skills, bool isArchived) : User(userId, firstName, rating, gender, skills, isArchived)
		{
			public new void SetUserId(long userId) { base.SetUserId(userId); }
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
				Skills skills = new(reader.IsDBNull(4) ? 1.0 : reader.GetDouble(4), reader.IsDBNull(5) ? 1.0 : reader.GetDouble(5), reader.IsDBNull(6) ? 1.0 : reader.GetDouble(6));

				_users.Add(new(userId, firstName, reader.IsDBNull(2) ? default : reader.GetDouble(2), gender, skills, reader.GetBoolean(7)));
			}

			return _users.Count - count;
		}

		public static bool TryUpdateRatingsAndSkills(SqliteDataReader reader, Func<int, bool>? checkCountCallback, out int count)
		{
			count = 0;
			const int FIELD_COUNT = 5;
			if (reader.FieldCount < FIELD_COUNT)
				throw new ArgumentException($"Количество полей в запросе должно быть не меньше {FIELD_COUNT}", nameof(reader));

			Dictionary<User, (double, Skills)> prevValues = new(_users.Count);
			TempUser equalValue = new(default, string.Empty, default, default, default, default);
			try
			{
				while (reader.Read())
				{
					long userId = reader.GetInt64(0);
					equalValue.SetUserId(userId);
					if (!_users.TryGetValue(equalValue, out User? user))
						throw new ArgumentException($"Невозмонно обновить рейтинги и навыки, т.к. не удалось найти игрока с ID {userId}", nameof(reader));

					prevValues.Add(user, (user.Rating, user.Skills));
					double rating = reader.GetDouble(1);
					Skills skills = new(reader.GetDouble(2), reader.GetDouble(3), reader.GetDouble(4));
					user.SetRatingAndSkills(rating, skills);
					count++;
				}

				if (checkCountCallback is not null && !checkCountCallback.Invoke(count))
					throw new ArgumentException($"Количество обновлённых игроков ({count}) не соответствует требуемому", nameof(checkCountCallback));
			}
			catch (Exception ex)
			{
				foreach (User user in prevValues.Keys)
					user.SetRatingAndSkills(prevValues[user].Item1, prevValues[user].Item2);

				count = 0;
				Console.Error.WriteLine("Не удалось обновить рейтинги игроков: " + ex.Message);
				return false;
			}

			return true;
		}

		public static IReadOnlyCollection<Player> GetPlayers() => _users;
		public static Player GetPlayer(long userId, string firstName, string? username = null, bool mustUpdateFirstName = true)
		{
			User equalValue = new(userId, firstName, default, default, default, default);
			if (!_users.TryGetValue(equalValue, out User? result))
				return equalValue;

			static bool EndsWithEmoji(string s)
			{
				string element = StringInfo.GetNextTextElement(s, s.Length - 1);
				var rune = element.EnumerateRunes().Last();
				return Rune.IsSymbol(rune);
			}

			if (!string.IsNullOrEmpty(username))
				result.Username = username;
			if (mustUpdateFirstName && firstName != result.FirstName && !string.IsNullOrEmpty(firstName) && !(firstName.StartsWith(result.FirstName) && firstName.Length == result.FirstName.Length + 2 &&
				EndsWithEmoji(firstName)))
			{
				Console.WriteLine($"Имя {result} обновлено на {firstName}");
				result.FirstName = firstName;
			}

			return result;
		}

		public static List<Player> GetPlayers(params IReadOnlyCollection<long> ids)
		{
			List<Player> result = new(ids.Count);
			TempUser equalValue = new(default, string.Empty, default, default, default, default);
			foreach (long id in ids)
			{
				equalValue.SetUserId(id);
				if (!_users.TryGetValue(equalValue, out User? user))
					continue;

				result.Add(user);
			}

			return result;
		}
	}
}