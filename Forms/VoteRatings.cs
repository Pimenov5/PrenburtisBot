using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text;
using TelegramBotBase.Form;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Args;

namespace PrenburtisBot.Forms
{
	[BotCommand("Заполнить рейтинги игроков")]
	internal class VoteRatings : BotCommandFormBase
	{
		private static List<Player>? s_players;
		private static SqliteConnection? s_connection;

		private static void BuildConnection()
		{
			if (s_connection is not null)
				return;

			const string PRENBURTIS_DATA_BASE = "PRENBURTIS_DATA_BASE";
			string path = Environment.GetEnvironmentVariable(PRENBURTIS_DATA_BASE)
				?? throw new NullReferenceException($"Отсутствует значение переменной окружения \"{PRENBURTIS_DATA_BASE}\"");

			if (!System.IO.File.Exists(path))
				throw new FileNotFoundException($"Отсутствует файл БД с путём: {path}");

			SqliteConnectionStringBuilder builder = new() { Mode = SqliteOpenMode.ReadWrite, DataSource = path };
			s_connection = new(builder.ConnectionString);
			s_connection.Open();
		}

		private static void CheckFieldCount(SqliteDataReader reader, int fieldCount)
		{
			if (reader.FieldCount != fieldCount)
				throw new Exception($"Количество полей в результате должно быть равно {fieldCount}, а не {reader.FieldCount}");
		}

		private static int GetFormId()
		{
			using SqliteCommand command = new("SELECT max(id) FROM ratings_forms WHERE closed_timestamp is NULL", s_connection ?? throw new NullReferenceException());
			using SqliteDataReader reader = command.ExecuteReader();
			if (!(reader.HasRows && reader.Read()))
				throw new Exception("Отсутствует открытая форма для заполнения");

			VoteRatings.CheckFieldCount(reader, 1);
			return reader.GetInt32(0);
		}

		private static DateTime? GetUserFormTimestamp(int formId, long userId)
		{
			using SqliteCommand command = new($"SELECT timestamp FROM ratings_forms_users WHERE telegram_id = {userId} AND ratings_form_id = {formId}",
				s_connection ?? throw new NullReferenceException());
			using SqliteDataReader reader = command.ExecuteReader();
			if (!(reader.HasRows && reader.Read()))
				return null;

			VoteRatings.CheckFieldCount(reader, 1);
			return reader.GetDateTime(0);
		}

		private int? _formId, _messageId;
		private bool? _isConfirmed;
		private readonly Dictionary<Player, int> _votes = [];

		private async Task TryDeleteMessage()
		{
			if (_messageId is not int messageId)
				return;

			await this.Device.DeleteMessage(messageId);
			_messageId = null;
		}

		private int WriteVotes(long userId)
		{
			static long GetFormUserId(int formId, long userId, SqliteTransaction transaction)
			{
				using SqliteCommand command = new($"INSERT INTO ratings_forms_users (telegram_id, ratings_form_id) VALUES ({userId}, {formId}) RETURNING id",
					s_connection ?? throw new NullReferenceException(), transaction);
				if (command.ExecuteScalar() is not object formUserId)
					throw new Exception("Не удалось выполнить:" + Environment.NewLine + command.CommandText);
				return (long)formUserId;
			}

			if (_votes.Count == 0)
				throw new Exception("Отсутствуют рейтинги игроков для записи");

			StringBuilder stringBuilder = new("INSERT INTO ratings_forms_users_votes (form_user_id, telegram_id, rating) VALUES ");
			const string VALUES_DELIMITER = ", ";
			foreach (KeyValuePair<Player, int> vote in _votes)
				stringBuilder.Append("({0}, " + $"{vote.Key.UserId}, {vote.Value})" + VALUES_DELIMITER);
			stringBuilder.Remove(stringBuilder.Length - VALUES_DELIMITER.Length, VALUES_DELIMITER.Length);
			stringBuilder.Append(" RETURNING *");

			string path = Path.Combine(
					Path.GetDirectoryName((s_connection ?? throw new NullReferenceException()).DataSource) ?? throw new NullReferenceException(),
					Path.GetRandomFileName()
				);
			using (FileStream fs = System.IO.File.Create(path, 1))
			{
				Console.Write(path);
			}

			int count = default;
			using SqliteTransaction transaction = (s_connection ?? throw new NullReferenceException()).BeginTransaction();
			try
			{
				long formUserId = GetFormUserId(_formId ?? throw new NullReferenceException(), userId, transaction);

				using SqliteCommand command = new(string.Format(stringBuilder.ToString(), formUserId), s_connection ?? throw new NullReferenceException(), transaction);
				using SqliteDataReader reader = command.ExecuteReader();
				if (!reader.HasRows)
					throw new Exception("Не удалось выполнить:" + Environment.NewLine + command.CommandText);

				while (reader.Read())
					count++;

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}

			return count;
		}

		public async Task<TextMessage> RenderAsync(long userId) => await RenderAsync(userId, null);
		public async Task<TextMessage> RenderAsync(long userId, string? strRating)
		{
			if (_isConfirmed is bool isConfirmed)
			{
				_isConfirmed = null;
				if (isConfirmed)
				{
					int count = this.WriteVotes(userId);
					return new TextMessage($"Количество записанных ответов: {count}").NavigateToStart();
				}
				else
				{
					await this.TryDeleteMessage();
					return new(string.Empty);
				}
			}

			BuildConnection();
			_formId ??= VoteRatings.GetFormId();
			if (_votes.Count == 0 && VoteRatings.GetUserFormTimestamp(_formId ?? throw new NullReferenceException(), userId) is DateTime timestamp)
				throw new Exception($"Вы уже отправили рейтинги игроков в {timestamp}");

			s_players ??= new(Users.GetPlayers());
			s_players.Sort((Player x, Player y) => x.FirstName.CompareTo(y.FirstName));
			if (s_players is null || s_players.Count == 0)
				throw new Exception("Отсутствуют данные игроков для заполнения");

			Player? userAsPlayer = null;
			foreach (Player player in s_players)
				if (player.UserId == userId)
				{
					userAsPlayer = player;
					break;
				}
			if (userAsPlayer is null)
				throw new Exception("Только добавленные в БД игроки могут заполнять рейтинги");

			if (!userAsPlayer.IsActual)
				throw new Exception("Только активные игроки могут заполнять рейтинги");

			if (_votes.Count == 0)
			{
				await this.Device.Send("Вам будут отправляться игроки в алфавитном порядке. На каждое сообщение с именем игрока вы должны отправить его рейтинг от 1 до 10, где 1 —"
					+ " это самый слабый игрок, а 10 — самый сильный только среди всех остальных, а не абсолютный уровень игры. Поэтому у вас ОБЯЗАТЕЛЬНО ДОЛЖНЫ БЫТЬ все оценки от 1 до 10,"
					+ " иначе результаты не будет приняты. Перед сохранением результатов вы сможете проверить все отправленные значения.");
			}
			else if (!string.IsNullOrEmpty(strRating))
			{
				if (!int.TryParse(strRating, out int rating) || rating <= 0 || rating > 10)
					throw new ArgumentException($"\"{strRating}\" не является рейтингом (от 1 до 10)");

				if (this.Device.LastMessage.ReplyToMessage is Message repliedMessage && repliedMessage.Text is string messageText)
				{
					foreach (Player player in s_players)
						if (player.FirstName.Equals(messageText))
							_votes[player] = rating;
				}
				else
				{
					foreach (Player player in _votes.Keys)
						if (_votes[player] == default)
						{
							_votes[player] = rating;
							break;
						}
				}
			}

			foreach (Player player in s_players)
				if (player.IsActual && player != userAsPlayer && (_votes.TryAdd(player, default) || _votes[player] == default))
					return new TextMessage(player.ToString()) { ParseMode = ParseMode.Markdown };

			bool hasAllRatings = true;
			StringBuilder stringBuilder = new();
			for (int i = 10; i > 0; i--)
			{
				stringBuilder.Append($"{i} — ");
				List<Player> players = _votes.Where((KeyValuePair<Player, int> pair) => pair.Value == i).ToList().ConvertAll<Player>((KeyValuePair<Player, int> pair) => pair.Key);
				if (players.Count == 0)
				{
					hasAllRatings = false;
					stringBuilder.Append('?');
				}
				else
					stringBuilder.AppendJoin(", ", players);
				stringBuilder.AppendLine();
			}

			stringBuilder.AppendLine(Environment.NewLine + (hasAllRatings ? string.Empty : $"Ваши ответы не могут быть сохранены, т.к. вы не присвоили все оценки (от 1 до 10). ")
				+ "Чтобы изменить рейтинг конкретного игрока, ответьте на сообщение с его именем новым значением рейтинга (от 1 до 10)");

			await this.TryDeleteMessage();
			Message message = await this.Device.Client.TelegramClient.SendTextMessageAsync(this.Device.DeviceId, stringBuilder.ToString(), parseMode: ParseMode.Markdown);
			_messageId = message.MessageId;

			if (!hasAllRatings)
				return new(string.Empty);

			ConfirmDialog confirmDialog = new("Сохранить ваши ответы? После этого уже нельзя будет внести изменения", [new("Сохранить и закрыть", bool.TrueString),
				new("Вернуться и редактировать", bool.FalseString)]) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs eventArgs) =>
			{
				_isConfirmed = bool.Parse(eventArgs.Button.Value);
				await confirmDialog.NavigateTo(this);
			};

			await this.NavigateTo(confirmDialog);
			return new(string.Empty);
		}
	}
}