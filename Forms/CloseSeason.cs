using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Закрыть абонемент", "BOT_OWNER_CHAT_ID")]
	internal class CloseSeason : SqliteBotCommandFormBase
	{
		private int? _seasonId = null;
		private bool? _isConfirmed = null;

		public static string GetCsvString(SortedDictionary<Player, HashSet<DateOnly>> players, char separator, Dictionary<Player, double> extras)
		{
			SortedDictionary<DateOnly, int> dates = [];
			foreach (var pair in players)
				foreach (DateOnly dateOnly in pair.Value)
				{
					if (dates.TryGetValue(dateOnly, out int playersCount))
						dates[dateOnly] = playersCount + 1;
					else
						dates.Add(dateOnly, 1);
				}

			List<string[]> lines = new() { Capacity = players.Count + 3 };
			string[] line = [string.Empty, "Всего игр", "Стоимость", extras.Count == 0 ? string.Empty : "Доплата"];
			int prevLength = line.Length;

			Array.Resize(ref line, line.Length + dates.Count);

			const int START_INDEX = 1;
			for (int i = START_INDEX; i < prevLength; i++)
				line[i + dates.Count] = line[i];

			int index = START_INDEX;
			foreach (DateOnly dateOnly in dates.Keys)
				line[index++] = dateOnly.ToString("ddd");

			lines.Add(line);
			line = new string[line.Length];
			index = START_INDEX;
			foreach (DateOnly dateOnly in dates.Keys)
				line[index++] = dateOnly.ToString("dd.MM");

			const string SEASON_PRICE = "SEASON_PRICE";
			if (Environment.GetEnvironmentVariable(SEASON_PRICE) is not string seasonPriceStr || !double.TryParse(seasonPriceStr, out double seasonPrice))
				throw new EnvVariableException(SEASON_PRICE);

			double price = Math.Round((seasonPrice - extras.Sum((pair) => pair.Value)) / dates.Sum((pair) => pair.Value), 2, MidpointRounding.AwayFromZero);

			lines.Insert(0, line);
			foreach (Player player in players.Keys)
			{
				line = new string[line.Length];
				line[0] = player.FirstName;
				line[^3] = players[player].Count.ToString();
				line[^2] = Math.Round(players[player].Count * price, 2, MidpointRounding.AwayFromZero).ToString();
				line[^1] = extras.TryGetValue(player, out double extra) ? extra.ToString() : string.Empty;

				index = START_INDEX;
				foreach (DateOnly dateOnly in dates.Keys)
					line[index++] = players[player].Contains(dateOnly) ? "+" : string.Empty;

				lines.Add(line);
			}

			foreach (KeyValuePair<Player, double> pair in extras)
				if (!players.ContainsKey(pair.Key))
				{
					line = new string[line.Length];
					line[0] = pair.Key.FirstName;
					line[^1] = pair.Value.ToString();

					lines.Add(line);
				}

			line = new string[line.Length];
			line[0] = "Количество игроков";
			int sum = dates.Sum((KeyValuePair) => KeyValuePair.Value);
			line[^3] = sum.ToString();
			double totalSum = Math.Round(sum * price, 2);
			line[^2] = totalSum.ToString();
			line[^1] = extras.Count == 0 ? string.Empty : (totalSum + extras.Sum((pair) => pair.Value)).ToString();

			index = START_INDEX;
			foreach (DateOnly date in dates.Keys)
				line[index++] = dates[date].ToString();

			lines.Add(line);
			lines.Add(["Цена 1 тренировки", Math.Round(price, 2).ToString()]);

			return Csv.CsvWriter.WriteToText(lines, separator);
		}

		public static Dictionary<Player, double> ReadExtrasFromDb(int seasonId)
		{
			Dictionary<Player, double> result = [];
			using SqliteCommand extrasCommand = new($"SELECT telegram_id, extra FROM seasons_extras WHERE season_id = {seasonId}", SqliteConnection);
			using SqliteDataReader extrasReader = extrasCommand.ExecuteReader();
			while (extrasReader.Read())
			{
				long userId = extrasReader.GetInt64(0);
				double extra = extrasReader.GetDouble(1);

				Player player = Users.GetPlayer(userId, string.Empty);
				if (string.IsNullOrEmpty(player.FirstName))
					throw new Exception($"При чтении таблицы доплат из БД не удалось найти пользователя с ID {userId}");

				result.Add(player, extra);
			}

			return result;
		}

		public async Task<TextMessage?> RenderAsync()
		{
			if (_isConfirmed is bool isConfirmed)
			{
				if (!isConfirmed)
					return new TextMessage(string.Empty).NavigateToStart();

				using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
				try
				{
					using SqliteCommand updateCommand = new($"UPDATE seasons SET closed_timestamp = \"{(DateTime.UtcNow.ToString(Environment.GetEnvironmentVariable("DB_DATE_FORMAT") 
						?? "yyyy-MM-dd") + " hh:mm:ss")}\" WHERE id = {_seasonId}", SqliteConnection, transaction);
					using SqliteDataReader updateReader = updateCommand.ExecuteReader();
					if (updateReader.RecordsAffected != 1)
						throw new($"При закрытии абонемента количество обновлённых строк должно быть равно 1, а не {updateReader.RecordsAffected}");

					transaction.Commit();
					return new TextMessage($"Абонемент с ID {_seasonId} успешно закрыт").NavigateToStart();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}
			}

			using SqliteCommand seasonCommand = new("SELECT id FROM seasons WHERE closed_timestamp IS NULL AND id = (SELECT MAX(id) FROM seasons)", SqliteConnection);
			using SqliteDataReader seasonReader = seasonCommand.ExecuteReader();
			if (!seasonReader.Read())
				return new("Не удалось найти открытый абонемент");

			_seasonId = seasonReader.GetInt32(0);
			using SqliteCommand datesCommand = new($"SELECT \"date\", telegram_id FROM seasons_days WHERE season_id = {_seasonId} ORDER BY \"date\"", SqliteConnection);
			using SqliteDataReader datesReader = datesCommand.ExecuteReader();

			SortedDictionary<Player, HashSet<DateOnly>> players = new(Comparer<Player>.Create((Player x, Player y) => x.FirstName.CompareTo(y.FirstName)));
			while (datesReader.Read())
			{
				DateOnly date = DateOnly.FromDateTime(datesReader.GetDateTime(0));

				long userId = datesReader.GetInt64(1);
				Player player = Users.GetPlayer(userId, string.Empty);
				if (string.IsNullOrEmpty(player.FirstName))
					return new($"Не удалось найти постоянного игрока с ID {userId}");

				if (!players.ContainsKey(player))
					players.Add(player, []);

				players[player].Add(date);
			}

			if (players.Count == 0)
				return new($"Не удалось найти записи на абонемент с ID {_seasonId}");

			Dictionary<Player, double> extras = CloseSeason.ReadExtrasFromDb(_seasonId ?? throw new NullReferenceException());
			string csvString = GetCsvString(players, '	', extras);

			ConfirmDialog confirmDialog = new($"Закрыть запись в абонемент с ID {_seasonId}?", new("Закрыть", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object sender, ButtonClickedEventArgs eventArgs) =>
			{
				_isConfirmed = bool.Parse(eventArgs.Button.Value);
				await confirmDialog.NavigateTo(this);
			};

			await this.Device.SendTextFile($"Season ID {_seasonId} from DB.csv", csvString, caption: $"CSV-таблица абонемента с ID {_seasonId} из БД");
			await this.NavigateTo(confirmDialog);
			return null;
		}
	}
}