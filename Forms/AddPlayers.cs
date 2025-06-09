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

		private static int WriteAttendance(long userId, Court court)
		{
			int result = default, count = default;

			const string VALUES_DELIMITER = ", ";
			StringBuilder stringBuilder = new("INSERT INTO attendance_users (attendance_id, telegram_id) VALUES ");
			foreach (Team team in court.Teams)
				foreach (Player player in team.Players)
				{
					count++;
					stringBuilder.Append("({0}, " + $"{player.UserId})" + VALUES_DELIMITER);
				}

			if (count == 0)
			{
				Console.WriteLine($"Невозможно записать посещаемость, т.к. на площадке (ID {Courts.IndexOf(court)}) нет игроков");
				return result;
			}

			stringBuilder.Remove(stringBuilder.Length - VALUES_DELIMITER.Length, VALUES_DELIMITER.Length);

			if (s_connection is null)
			{
				SqliteConnectionStringBuilder connectionStringBuilder = new() { Mode = SqliteOpenMode.ReadWrite };
				s_connection = new(connectionStringBuilder.SetDataSource("PRENBURTIS_DATA_BASE").ConnectionString);
			}

			try
			{
				s_connection.Open();
				using SqliteTransaction transaction = s_connection.BeginTransaction();
				try
				{
					List<TimeOnly>? times = [];
					using (SqliteCommand selectCommand = new($"SELECT timestamp FROM attendance WHERE attendance.telegram_id = {userId}"
						+ $" AND date(attendance.timestamp) = \"{DateTimeOffset.UtcNow.Date.ToString(Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd")}\"",
						s_connection, transaction))
					{
						using SqliteDataReader selectReader = selectCommand.ExecuteReader();
						while (selectReader.Read())
							times.Add(TimeOnly.FromDateTime(selectReader.GetDateTime(0)));
					}

					if (times.Count > 0)
					{
						Console.WriteLine($"Пользователь (ID {userId}) уже записал посещаемость сегодня{(times.Count > 1 ? ": " : " в ") + new StringBuilder().AppendJoin(", ", times)}");
						return result;
					}

					long attendanceId = default;
					using (SqliteCommand attendanceCommand = new($"INSERT INTO attendance (telegram_id) VALUES ({userId}) RETURNING id", s_connection, transaction))
					{
						if (attendanceCommand.ExecuteScalar() is not object attendanceCommandResult)
							throw new Exception("Не удалось выполнить: " + attendanceCommand.CommandText);
						attendanceId = (long)attendanceCommandResult;
					}

					string commandText = string.Format(stringBuilder.ToString(), attendanceId);
					using (SqliteCommand insertCommand = new(commandText, s_connection, transaction))
					{
						using SqliteDataReader insertReader = insertCommand.ExecuteReader();
						result = insertReader.RecordsAffected;
					}

					if (result == count)
						transaction.Commit();
					else
						throw new Exception("Данные не могут быть сохранены, т.к. количество внесённых в БД записей"
							+ result switch { 0 => " равно 0", _ => $"({result}) не равно количеству игроков на площадке ({count})" });
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
				if (Environment.GetEnvironmentVariable("WRITE_ATTENDANCE") is string value && bool.TryParse(value, out bool needWrite) && needWrite)
					Console.WriteLine($"Количество записанных в посещаемость игроков: {AddPlayers.WriteAttendance(userId, court)}");
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