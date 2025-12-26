using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text;
using TelegramBotBase.Form;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Args;
using System.Collections.ObjectModel;

namespace PrenburtisBot.Forms
{
	[BotCommand("Заполнить рейтинги игроков")]
	internal class VoteRatings : SqliteBotCommandFormBase
	{
		private class Form(long id, IDictionary<long, (bool, bool)> permissions)
		{
			public long Id = id;
			public ReadOnlyDictionary<long, (bool, bool)> Permissions = new(permissions);
		}

		private static List<Player>? s_sortedPlayers;
		private static Dictionary<string, Player>? s_players;

		private static Dictionary<string, Player> CreatePlayers(IReadOnlyCollection<Player> players, IDictionary<long, (bool, bool)> permissions)
		{
			if (players.Count == 0)
				throw new ArgumentException("Отсутствуют данные игроков для заполнения");

			Dictionary<string, Player> result = new(players.Count);
			foreach (Player player in players)
			{
				if (!permissions.TryGetValue(player.UserId, out (bool, bool) permission) || !permission.Item2)
					continue;

				foreach (Player item in players)
					if (player != item && player.FirstName.Equals(item.FirstName))
						throw new ArgumentException($"Пользователи с одинаковыми именами: {player} и {item}", nameof(players));

				result.Add(player.ToString(), player);
			}

			if (result.Count == 0)
				throw new Exception("Список игроков для заполнения пуст");

			return result;
		}

		private static Form GetForm()
		{
			using SqliteCommand command = new("SELECT MAX(id), opened_timestamp FROM ratings_forms WHERE closed_timestamp is NULL", SqliteConnection);
			using SqliteDataReader formReader = command.ExecuteReader();
			if (!formReader.Read())
				throw new Exception("Отсутствует открытая для заполнения форма");
			if (formReader.GetDateTime(1) is DateTime timestamp && DateTime.UtcNow < timestamp)
				throw new Exception($"Форма голосования будет открыта с {timestamp} (UTC)");

			long id = formReader.GetInt64(0);
			formReader.Close();
			command.CommandText = "SELECT telegram_id, can_vote, need_vote FROM ratings_forms_permissions WHERE ratings_form_id = " + id.ToString();
			using SqliteDataReader reader = command.ExecuteReader();

			Dictionary<long, (bool, bool)> permissions = [];
			while (reader.Read())
				permissions.Add(reader.GetInt64(0), (reader.GetBoolean(1), reader.GetBoolean(2)));

			if (permissions.Count == 0)
				throw new Exception("Отсутствуют разрешения для формы голосования с ID " + id.ToString());

			return new(id, permissions);
		}

		private Form? _form;
		private int? _lastMessageId;
		private bool? _isConfirmed;
		private readonly Dictionary<Player, int> _votes = [];

		private async Task TryDeleteLastMessage()
		{
			if (_lastMessageId is not int messageId)
				return;

			await this.Device.DeleteMessage(messageId);
			_lastMessageId = null;
		}

		private int WriteVotes(long userId)
		{
			static long GetFormUserId(long formId, long userId, SqliteTransaction transaction)
			{
				using SqliteCommand command = new($"INSERT INTO ratings_forms_users (telegram_id, ratings_form_id) VALUES ({userId}, {formId}) RETURNING id",
					SqliteConnection, transaction);
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

			int count = default;
			using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				long formUserId = GetFormUserId(_form?.Id ?? throw new NullReferenceException(), userId, transaction);

				using SqliteCommand command = new(string.Format(stringBuilder.ToString(), formUserId), SqliteConnection, transaction);
				using SqliteDataReader reader = command.ExecuteReader();
				count = reader.RecordsAffected;

				if (count == _votes.Count)
					transaction.Commit();
				else 
					throw new Exception("Данные не могут быть сохранены, т.к. количество внесённых в БД записей" 
						+ count switch { 0 => " равно 0", _ => $"({count}) не равно количеству ваших ответов ({_votes.Count})" });
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
					await this.TryDeleteLastMessage();
					return new(string.Empty);
				}
			}

			_form ??= VoteRatings.GetForm();
			using (SqliteCommand timestampCommand = new($"SELECT timestamp FROM ratings_forms_users WHERE telegram_id = {userId} AND ratings_form_id = {_form.Id}", SqliteConnection))
			{
				DateTime? timestamp = (DateTime?)timestampCommand.ExecuteScalar();
				if (_votes.Count == 0 && timestamp is not null)
					throw new Exception($"Вы уже отправили рейтинги игроков в {timestamp}");
			}

			bool needSort = s_sortedPlayers is null;
			s_sortedPlayers ??= [..Users.GetPlayers()];
			s_sortedPlayers.RemoveAll((Player player) => !_form.Permissions.TryGetValue(player.UserId, out (bool, bool) permission) || (!permission.Item1 && !permission.Item2));
			if (s_sortedPlayers.Count == 0)
				throw new InvalidOperationException("Список постоянных активных игроков пуст");
			if (needSort)
				s_sortedPlayers.Sort((Player x, Player y) => x.FirstName.CompareTo(y.FirstName));

			Player? userAsPlayer = null;
			foreach (Player player in s_sortedPlayers)
				if (player.UserId == userId)
				{
					userAsPlayer = player;
					break;
				}

			if (userAsPlayer is null || !_form.Permissions.TryGetValue(userAsPlayer.UserId, out (bool, bool) permission) || !permission.Item1)
				throw new Exception("Только постоянные игроки с допуском могут заполнять рейтинги");

			s_players ??= VoteRatings.CreatePlayers(s_sortedPlayers, _form.Permissions);

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
					if (s_players.Keys.Where((string key) => key.Contains(messageText)).ToList() is List<string> keys && keys.Count == 1 && s_players[keys[0]] is Player player)
						_votes[player] = rating;
					else
						await this.Device.Send($"\"{messageText}\" не является именем игрока");
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

			foreach (Player player in s_sortedPlayers)
				if (player != userAsPlayer && s_players.ContainsValue(player) && (_votes.TryAdd(player, default) || _votes[player] == default))
				{
					string? text = null;
					foreach (string key in s_players.Keys) 
						if (s_players[key] == player)
						{
							text = key;
							break;
						}
					if (string.IsNullOrEmpty(text))
						throw new Exception($"Не удалось найти уникальное имя игрока {player}");

					return new TextMessage(text) { ParseMode = ParseMode.Markdown };
				}

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

			await this.TryDeleteLastMessage();
			Message message = await this.API.SendMessage(this.Device.DeviceId, stringBuilder.ToString(), parseMode: ParseMode.Markdown);
			_lastMessageId = message.MessageId;

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