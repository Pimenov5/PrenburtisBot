using System.Text;
using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TL;
using Microsoft.Data.Sqlite;

namespace PrenburtisBot.Forms
{
	[BotCommand("Добавить к площадке игроков из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class AddPlayers : RepliedToPollGroupFormBase
	{
		private string? _courtId;
		private static SqliteConnection? s_connection;

		private enum Attendance { Insert, Update };
		private static int WriteAttendance(long userId, Court court, Attendance attendance)
		{
			List<long> idsToInsert = [];
			foreach (Team team in court.Teams)
				foreach (Player player in team.Players)
					idsToInsert.Add(player.UserId);

			if (idsToInsert.Count == 0)
			{
				Console.WriteLine($"Невозможно записать посещаемость, т.к. на площадке (ID {Courts.IndexOf(court)}) нет игроков");
				return default;
			}

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
					Dictionary<long, TimeOnly>? times = [];
					using (SqliteCommand selectCommand = new($"SELECT id, timestamp FROM attendance WHERE telegram_id = {userId} AND date(attendance.timestamp)"
						+ $"= \"{DateTimeOffset.UtcNow.Date.ToString(Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd")}\"", s_connection, transaction))
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
						using SqliteCommand attendanceCommand = new($"INSERT INTO attendance (telegram_id) VALUES ({userId}) RETURNING id", s_connection, transaction);
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
						foreach (Team team in court.Teams)
							foreach (Player player in team.Players)
							{
								if (idsToInsert.Contains(player.UserId) && !Users.GetPlayers().Contains(player))
								{
									usersToInsert ??= [];
									usersToInsert.Add(player);
								};
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

		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			_courtId = args.Length >= 1 ? args[0] : null;
			Court court = this.GetCourt(players.Count, userId, out int courtId);

			uint?[] indexes = court.AddPlayers(players);
			int count = 0;
			foreach (uint? index in indexes)
				count = index is null ? count + 1 : count;

			if (count != 0)
				throw new InvalidOperationException($"Не удалось добавить {count} из {indexes.Length} игроков");

			try
			{
				Attendance? attendanceOrNull = null;
				if (args.Length > 0)
				{
					string argument = args[args.Length >= 2 ? 1 : 0];
					attendanceOrNull = argument.ToUpper() switch
					{
						"INSERT" => Attendance.Insert,
						"UPDATE" => Attendance.Update,
						_ => throw new ArgumentException($"\"{argument}\" не является указанием как записать посещаемость", nameof(args))
					};
				}
				else if (Environment.GetEnvironmentVariable("INSERT_ATTENDANCE") is string strValue && bool.TryParse(strValue, out bool boolValue) && boolValue)
					attendanceOrNull = Attendance.Insert;
				
				if (attendanceOrNull is Attendance attendance)
					Console.WriteLine($"Количество изменённых в посещаемости строк: {AddPlayers.WriteAttendance(userId, court, attendance)}");
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.ToString());
			}

			string text = CourtPlayers.ToString(court, userId, this.Device.IsGroup);
			return new TextMessage(text) { ParseMode = ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET);
		}

		protected virtual Court GetCourt(int playersCount, long userId, out int courtId)
		{
			Court court;
			try
			{
				court = Courts.GetById(ref _courtId, userId);
			}
			catch
			{
				if (string.IsNullOrEmpty(_courtId))
					throw new ArgumentNullException(nameof(_courtId), "Вы не указали идентификатор площадки");
				else if (!uint.TryParse(_courtId, out uint value))
					throw new ArgumentOutOfRangeException(nameof(_courtId), $"\"{_courtId}\" не является валидным идентификатором площадки");
				else
					throw new ArgumentOutOfRangeException(nameof(_courtId), $"Не удалось найти площадку с идентификатором {_courtId}");
			}

			int count = 0;
			foreach (Team team in court.Teams)
				count += team.PlayerCount;

			long maxCount = court.Teams.Length * court.TeamMaxPlayerCount;
			if (playersCount > maxCount - count)
				throw new InvalidOperationException($"Недостаточно свободных мест на площадке ({maxCount - count}) для добавления игроков из опроса ({playersCount})");

			courtId = int.Parse(_courtId ?? throw new NullReferenceException());
			return court;
		}
	}
}