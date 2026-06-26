using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Extensions;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Args;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[NeededTelegramClient]
	[BotCommand("Заполнить абонемент из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class SeasonFromPoll : BotCommandGroupFormBase
	{
		private static WTelegram.Client? s_client;
		private static SqliteConnection? s_connection;

		private bool? _isConfirmed = null;
		private int? _seasonId = null, _repliedMessageId = null;
		private readonly SortedDictionary<Player, HashSet<DateOnly>> _result = new(Comparer<Player>.Create((x, y) => x.FirstName.CompareTo(y.FirstName)));

		private int InsertPlayersToDataBase()
		{
			if (s_connection is null)
				throw new NullReferenceException("Невозможно записать дни тренировок в абонемент, т.к. отсутствует подключение к БД");

			int count = 0;
			foreach (var pair in _result)
				count += pair.Value.Count;

			if (count == 0)
				throw new InvalidOperationException("Невозможно записать дни тренировок в абонемент, т.к. отсутствуют данные для записи");

			SqliteTransaction transaction = s_connection.BeginTransaction();
			try
			{
				StringBuilder stringBuilder = new($"SELECT DISTINCT telegram_id FROM seasons_days WHERE season_id = {_seasonId} AND telegram_id IN (");
				stringBuilder.AppendJoin(',', _result.Keys.Select((Player player, int index) => player.UserId)).Append(')');

				using SqliteCommand selectCommand = new(stringBuilder.ToString(), s_connection, transaction);
				using SqliteDataReader selectReader = selectCommand.ExecuteReader();
				List<Player> players = [];
				while (selectReader.Read())
				{
					long userId = selectReader.GetInt64(0);
					Player player = Users.GetPlayer(userId, string.Empty);
					players.Add(player);
				}

				if (players.Count > 0)
				{
					throw new($"Невозможно заполнить абонемент (ID {_seasonId}), т.к. следующие пользователи уже записались: " 
						+ string.Join(", ", players.ConvertAll((Player player) => player.FirstName)));
				}

				stringBuilder.Clear();
				stringBuilder.Append("INSERT INTO seasons_days (season_id, telegram_id, date) VALUES ");
				string dateFormat = Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd";

				int i = 0;
				foreach (Player key in _result.Keys)
				{
					stringBuilder.AppendJoin(',', _result[key].ToList().ConvertAll((dateOnly) => $"({_seasonId}, {key.UserId}, \"{dateOnly.ToString(dateFormat)}\")"));
					if (++i < _result.Count)
						stringBuilder.Append(',');
				}

				using SqliteCommand insertCommand = new(stringBuilder.ToString(), s_connection, transaction);
				using SqliteDataReader insertReader = insertCommand.ExecuteReader();
				if (insertReader.RecordsAffected != count)
					throw new Exception($"Количество добавленных строк ({insertReader.RecordsAffected}) не равно количеству записей в абонемент ({_result.Count})");

				transaction.Commit();
				return insertReader.RecordsAffected;
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		public static WTelegram.Client TelegramClient { set { s_client = value; } }

		public const string ALL_PLAYERS_ALIAS = "all";

		public async Task<TextMessage?> RenderAsync(MessageResult message)
		{
			if (_isConfirmed is bool isConfirmed)
			{
				_isConfirmed = null;
				if (!isConfirmed)
					return new TextMessage(string.Empty).NavigateToStart();

				int count = InsertPlayersToDataBase();
				return new TextMessage($"В абонемент (ID {_seasonId}) добавлено строк: {count}") { NavigateTo = new(new CloseSeason()), ReplyToMessageId = _repliedMessageId };
			}

			if (message.Message.ReplyToMessage is not Message repliedMessage || repliedMessage.Poll is not Poll poll || poll.IsAnonymous || poll.AllowsMultipleAnswers)
				return new("Команда должна вызываться в ответ на не анонимный опрос");

			_repliedMessageId = repliedMessage.MessageId;
			int capacity = 0;
			Dictionary<int, (List<string>, int)> votes = [];
			for (int i = 0; i < poll.Options.Length; i++)
			{
				PollOption option = poll.Options[i];
				if (option.VoterCount == 0)
					continue;

				string[] strArray = option.Text.Split(OpenSeason.DAYS_SEPARATOR);
				if (strArray.Length == 0)
					break;

				List<string> days = new(strArray.Length);
				foreach (string item in strArray)
					if (Enum.TryParse<DayOfWeek>(item, out _))
						days.Add(item);

				if (days.Count > 0)
				{
					capacity += option.VoterCount;
					votes.Add(i, (days, option.VoterCount));
				}
			}

			Dictionary<int, List<Player>> players = await (s_client
				?? throw new NullReferenceException("Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались")).GetPlayersFromPollAsync(repliedMessage);

			if (s_connection is null)
			{
				SqliteConnectionStringBuilder connectionStringBuilder = new() { Mode = SqliteOpenMode.ReadWrite };
				s_connection = new(connectionStringBuilder.SetDataSource("PRENBURTIS_DATA_BASE").ConnectionString);
				s_connection.Open();
			}

			Season season;
			DateOnly firstDate, lastDate;
			string dateFormat = Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd";

			using (SqliteCommand seasonCommand = new($"SELECT id, first_date, last_date FROM seasons WHERE \"{DateTime.UtcNow.ToString(dateFormat + "hh:mm:ss")}\" >= opened_timestamp "
				+ "AND closed_timestamp IS NULL AND id = (SELECT MAX(id) FROM seasons)", s_connection))
			{
				using SqliteDataReader seasonReader = seasonCommand.ExecuteReader();
				if (!seasonReader.Read())
					return new TextMessage("Не удалось найти открытый для записи абонемент").NavigateToStart();

				int seasonId = seasonReader.GetInt32(0);
				firstDate = DateOnly.FromDateTime(seasonReader.GetDateTime(1));
				lastDate = DateOnly.FromDateTime(seasonReader.GetDateTime(2));

				season = new(seasonId, firstDate, lastDate);
			}

			foreach (KeyValuePair<int, List<Player>> pair in players)
				if (votes.TryGetValue(pair.Key, out (List<string>, int) value))
				{
					HashSet<DateOnly> dates = [..season.ParseDates(value.Item1)];

					foreach (Player player in pair.Value)
						_result.Add(player, dates);
				}

			Dictionary<Player, double>? extras = null;
			if (message.BotCommandParameters.Count == 1 && message.BotCommandParameters[0].Equals(ALL_PLAYERS_ALIAS, StringComparison.OrdinalIgnoreCase))
			{
				using (SqliteCommand playersCommand = new($"SELECT telegram_id, \"date\" FROM seasons_days WHERE season_id = {season.Id} ORDER BY telegram_id", s_connection))
				{
					using SqliteDataReader playersReader = playersCommand.ExecuteReader();
					Player? player = null, prevPlayer = null;
					while (playersReader.Read())
					{
						long userId = playersReader.GetInt64(0);
						player = player is null || player.UserId != userId ? Users.GetPlayer(userId, string.Empty) : player;
						if (!_result.TryGetValue(player, out HashSet<DateOnly>? dates))
							_result.Add(player, []);
						else if (prevPlayer is null || prevPlayer != player)
							throw new ArgumentException($"Невозможно добавить даты из БД, т.к. игрок {player.FirstName} также выбрал дни посещений в опросе", nameof(message));

						DateOnly date = DateOnly.FromDateTime(playersReader.GetDateTime(1));
						_result[player].Add(date);
						prevPlayer = player;
					}
				}

				extras = CloseSeason.ReadExtrasFromDb(season.Id);
			}

			if (_result.Count == 0)
				return new("Количество записавшихся игроков равно 0");

			string csvString = CloseSeason.GetCsvString(_result, '	', extras is null ? [] : extras);
			await this.Device.SendTextFile($"Season ID {season.Id} from poll.csv", csvString, caption: $"CSV-таблица абонемента с ID {season.Id} из опроса", replyTo: repliedMessage.MessageId);

			ConfirmDialog confirmDialog = new("Сохранить расписание из опроса в БД?", new("Сохранить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object sender, ButtonClickedEventArgs eventArgs) =>
			{
				_seasonId = season.Id;
				_isConfirmed = bool.Parse(eventArgs.Button.Value);
				await confirmDialog.NavigateTo(this);
			};

			return new TextMessage(string.Empty) { NavigateTo = new(confirmDialog) };
		}
	}
}