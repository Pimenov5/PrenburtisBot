using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;

namespace PrenburtisBot.Forms
{
	public enum Attendance { Insert, Update };

	[BotCommand("Записать посещаемость", Telegram.Bot.Types.Enums.BotCommandScopeType.AllChatAdministrators)]
	internal class WriteAttendance : RepliedToPollGroupFormBase
	{
		private static SqliteConnection? s_connection;

		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			DateOnly? dateOnly = null;
			TimeOnly? timeOnly = null;
			List<string> argsList = [..args];

			for (int i = argsList.Count - 1; i >= 0; i--)
			{
				bool mustRemove = false;
				if (dateOnly is null && DateOnly.TryParse(args[i], out DateOnly parsedDate))
				{
					dateOnly = parsedDate;
					mustRemove = true;
				}
				else if (timeOnly is null && TimeOnly.TryParse(args[i], out TimeOnly parsedTime))
				{
					timeOnly = parsedTime;
					mustRemove = true;
				}

				if (mustRemove)
					argsList.RemoveAt(i);
			}

			DateTime? dateTime = dateOnly is not null ? dateOnly?.ToDateTime(timeOnly ?? TimeOnly.MinValue) : null;
			dateTime ??= timeOnly is not null ? DateOnly.FromDateTime(DateTime.Today).ToDateTime(timeOnly ?? TimeOnly.MinValue) : null;

			Attendance attendance = argsList.Count switch {
				0 => Attendance.Insert,
				_ => Enum.TryParse(typeof(Attendance), argsList[0], true, out object? parsedAttendance) ? (Attendance)parsedAttendance
					: throw new ArgumentException("Не удалось определить вызываемое действие", nameof(args)),
			};

			if (WriteAttendance.Write(userId, players, attendance, dateTime) is int count)
				Console.WriteLine($"Количество изменённых в посещаемости строк: {count}");

			return new TextMessage(string.Empty).NavigateToStart(Start.SET_QUIET);
		}

		public static int Write(long userId, IEnumerable<Player> players, Attendance attendance = Attendance.Insert, DateTime? dateTime = null)
		{
			List<long> idsToInsert = [];
			foreach (Player player in players)
				idsToInsert.Add(player.UserId);

			if (idsToInsert.Count == 0)
				throw new ArgumentException($"Невозможно записать нулевую посещаемость игроков", nameof(players));

			if (s_connection is null)
			{
				SqliteConnectionStringBuilder connectionStringBuilder = new() { Mode = SqliteOpenMode.ReadWrite };
				s_connection = new(connectionStringBuilder.SetDataSource("PRENBURTIS_DATA_BASE").ConnectionString);
			}

			int result = default;
			try
			{
				s_connection.Open();
				using SqliteTransaction transaction = s_connection.BeginTransaction();
				try
				{
					string dateTimeFormat = Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd";
					Dictionary<long, TimeOnly>? times = [];
					using (SqliteCommand selectCommand = new($"SELECT id, timestamp FROM attendance WHERE telegram_id = {userId} AND date(attendance.timestamp)"
						+ $"= \"{(dateTime ?? DateTimeOffset.UtcNow).Date.ToString(dateTimeFormat)}\"", s_connection, transaction))
					{
						using SqliteDataReader selectReader = selectCommand.ExecuteReader();
						while (selectReader.Read())
							times.Add(selectReader.GetInt64(0), TimeOnly.FromDateTime(selectReader.GetDateTime(1)));
					}

					long attendanceId = default;
					List<long> idsToDelete = [];
					if (times.Count > 0)
					{
						if (attendance == Attendance.Update)
						{
							List<KeyValuePair<long, TimeOnly>> list = [.. times];
							list.Sort((x, y) => y.Value.CompareTo(x.Value));
							attendanceId = list[0].Key;

							using SqliteCommand playersCommand = new($"SELECT telegram_id FROM attendance_users WHERE attendance_id = {attendanceId}", s_connection, transaction);
							using SqliteDataReader playersReader = playersCommand.ExecuteReader();
							List<long> dbPlayers = [];
							while (playersReader.Read() && playersReader.GetInt64(0) is long telegramId)
								if (!idsToInsert.Remove(telegramId))
									idsToDelete.Add(telegramId);

							if (idsToInsert.Count == 0 && idsToDelete.Count == 0)
							{
								Console.WriteLine($"Обновление посещаемости в {list[0].Value} (ID {attendanceId}) не требуется");
								return default;
							}
						}
						else
						{
							Console.WriteLine($"Пользователь (ID {userId}) уже записал посещаемость сегодня{(times.Count > 1 ? ": " : " в ") + new StringBuilder().AppendJoin(", ", times.Values)}");
							return default;
						}
					}

					if (attendanceId == default)
					{
						using SqliteCommand attendanceCommand = new($"INSERT INTO attendance (timestamp, telegram_id) VALUES ({(dateTime is null ? "current_timestamp"
							: '"' + dateTime?.ToString(dateTimeFormat + " HH:mm:ss") + '"')}, {userId}) RETURNING id", s_connection, transaction);
						if (attendanceCommand.ExecuteScalar() is not object attendanceCommandResult)
							throw new Exception("Не удалось выполнить: " + attendanceCommand.CommandText);
						attendanceId = (long)attendanceCommandResult;
					}
					else if (attendance == Attendance.Update)
					{
						using SqliteCommand updateCommand = new($"UPDATE attendance SET timestamp = current_timestamp WHERE id = {attendanceId}", s_connection, transaction);
						using SqliteDataReader updateReader = updateCommand.ExecuteReader();
						if (updateReader.RecordsAffected != 1)
							throw new Exception("Не удалось выполнить: " + updateCommand.CommandText);
					}

					if (idsToDelete.Count > 0)
					{
						using SqliteCommand deleteCommand = new($"DELETE FROM attendance_users WHERE attendance_id = {attendanceId} AND telegram_id IN ({new StringBuilder().AppendJoin(',', idsToDelete)})",
							s_connection, transaction);
						using SqliteDataReader deleteReader = deleteCommand.ExecuteReader();
						if (deleteReader.RecordsAffected != idsToDelete.Count)
							throw new Exception("Не удалось выполнить: " + deleteCommand.CommandText);
						result += deleteReader.RecordsAffected;
					}

					if (idsToInsert.Count > 0)
					{
						List<Player>? usersToInsert = null;
						foreach (Player player in players)
						{
							if (idsToInsert.Contains(player.UserId) && !Users.GetPlayers().Contains(player))
							{
								usersToInsert ??= [];
								usersToInsert.Add(player);
							}
							;
						}

						if (usersToInsert is not null)
						{
							string commandText = new StringBuilder("INSERT INTO users (telegram_id, first_name, comment) VALUES ").AppendJoin(',',
								usersToInsert.ConvertAll((Player player) => $"({player.UserId}, \"{player.FirstName}\", \"{userId} {DateTime.Now}\")")).ToString();
							using SqliteCommand insertUsers = new(commandText, s_connection, transaction);
							using SqliteDataReader usersReader = insertUsers.ExecuteReader();
							if (usersReader.RecordsAffected != usersToInsert.Count)
								throw new Exception("Не удалось выполнить: " + insertUsers.CommandText);
						}

						StringBuilder stringBuilder = new StringBuilder("INSERT INTO attendance_users (attendance_id, telegram_id) VALUES ").AppendJoin(',', idsToInsert.ConvertAll((long id) => $"({attendanceId},{id})"));
						using SqliteCommand insertCommand = new(stringBuilder.ToString(), s_connection, transaction);
						using SqliteDataReader insertReader = insertCommand.ExecuteReader();
						if (insertReader.RecordsAffected != idsToInsert.Count)
							throw new Exception("Не удалось выполнить: " + insertCommand.CommandText);
						result += insertReader.RecordsAffected;
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}
			}
			finally
			{
				s_connection.Close();
			}

			return result;
		}
	}
}