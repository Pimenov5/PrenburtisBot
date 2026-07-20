using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.BeforeBotStart;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
namespace PrenburtisBot.Forms
{
	[BotCommandChat("Закрыть форму голосования", "BOT_OWNER_CHAT_ID")]
	internal class CloseVoteRatings : SqliteBotCommandFormBase
	{
		private int _formId = default, _votedCount = default;
		private readonly List<long> _needVoteIds = [];

		public async Task<TextMessage?> RenderAsync()
		{
			using SqliteCommand formCommand = new("SELECT id, opened_timestamp FROM ratings_forms WHERE closed_timestamp IS NULL", SqliteConnection);
			using SqliteDataReader formReader = formCommand.ExecuteReader();

			DateTime? opened = null;
			const string ERROR_PREFIX = "Невозможно закрыть форму голосования, т.к. ";
			while (formReader.Read())
			{
				_formId = _formId == default ? formReader.GetInt32(0) : throw new(ERROR_PREFIX + "в БД несколько открытых форм");
				opened = formReader.GetDateTime(1);
			}

			if (opened is null)
				return new ErrorTextMessage(ERROR_PREFIX + "в БД не обнаружена открытая форма");
			else if (DateTime.UtcNow < opened)
				return new ErrorTextMessage(ERROR_PREFIX + $"голосование в форме ещё не стартовало ({opened})");

			using SqliteCommand permissionsCommand = new($"SELECT telegram_id, can_vote, need_vote FROM ratings_forms_permissions WHERE ratings_form_id = {_formId}", SqliteConnection);
			using SqliteDataReader permissionsReader = permissionsCommand.ExecuteReader();
			List<long> canVoteIds = [];
			while (permissionsReader.Read())
			{
				long userId = permissionsReader.GetInt64(0);
				bool canVote = permissionsReader.GetBoolean(1);
				if (canVote)
					canVoteIds.Add(userId);
				bool needVote = permissionsReader.GetBoolean(2);
				if (needVote)
					_needVoteIds.Add(userId);
			}

			using SqliteCommand votedCommand = new($"SELECT COUNT(telegram_id) FROM ratings_forms_users WHERE ratings_form_id = {_formId}", SqliteConnection);
			using SqliteDataReader votedReader = votedCommand.ExecuteReader();
			_votedCount = votedReader.Read() ? votedReader.GetInt32(0) : throw new(ERROR_PREFIX + "не удалось получить количество проголососвавших игрок");

			const double MIN_PERCENT_VOTED = 0.5; // 50%
			double minVoted = Math.Ceiling(canVoteIds.Count * MIN_PERCENT_VOTED);
			if (_votedCount < minVoted)
				return new ErrorTextMessage(ERROR_PREFIX + $"количество проголосовавших ({_votedCount}) меньше минимального требования: {MIN_PERCENT_VOTED} от {canVoteIds.Count} = {minVoted}");

			ConfirmDialog confirmDialog = new($"Количество проголосовавших в форме (ID {_formId}): {_votedCount} из {canVoteIds.Count}. Закрыть форму и записать её результаты?",
				new("Закрыть и сохранить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object sender, ButtonClickedEventArgs eventArgs) =>
			{
				await confirmDialog.NavigateTo(this, eventArgs.Button.Value);
			};

			await this.NavigateTo(confirmDialog);
			return null;
		}

		public async Task<TextMessage> RenderAsync(string strIsConfirmed)
		{
			if (!bool.Parse(strIsConfirmed))
				return new TextMessage(string.Empty).NavigateToStart();

			string commandText = "INSERT INTO ratings (telegram_id, rating, passing, setting, attacking) " +
				"SELECT users.telegram_id, ROUND((SELECT AVG(ratings_forms_users_votes.rating) " +
				"FROM ratings_forms_users_votes " +
				"JOIN ratings_forms_users ON ratings_forms_users.id = ratings_forms_users_votes.form_user_id " +
				$"JOIN ratings_forms ON ratings_forms.id = ratings_forms_users.ratings_form_id AND ratings_forms.id = {_formId} " +
				"WHERE ratings_forms_users_votes.telegram_id = users.telegram_id), 2) as new_rating, ratings.passing, ratings.setting, ratings.attacking " +
				"FROM users " +
				"JOIN ratings  ON ratings.telegram_id = users.telegram_id AND ratings.timestamp = (SELECT MAX(ratings.timestamp) FROM ratings " +
				"WHERE ratings.telegram_id = users.telegram_id) " +
				"WHERE new_rating IS NOT NULL " +
				"ORDER BY new_rating DESC";

			string closed = DateTime.UtcNow.ToString((Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd") + " hh:mm:ss");
			using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				using SqliteCommand insertCommand = new(commandText, SqliteConnection, transaction);
				insertCommand.ExecuteNonQuery();
				using SqliteCommand updateCommand = new($"UPDATE ratings_forms SET closed_timestamp = \"{closed}\" WHERE id = {_formId}", SqliteConnection, transaction);
				updateCommand.ExecuteNonQuery();
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}

			int count = ReadUsers.UpdateFromSqliteDb(_needVoteIds);
			List<Player> players = Users.GetPlayers(_needVoteIds);
			if (players.Count != _needVoteIds.Count)
				throw new($"Количество данных обновлённых игроков ({players.Count}) не соответствует значению из формы голосования ({_needVoteIds.Count})");

			players.Sort(Comparer<Player>.Create((Player a, Player b) => b.Rating.CompareTo(a.Rating)));
			StringBuilder stringBuilder = new($"Закрыта форма голосования (ID {_formId}), соотношение количества проголосовавших к списку игроков в форме "
				+ $"равно {_votedCount}/{_needVoteIds.Count}. Обновлённый рейтинг игроков:" + Environment.NewLine + Environment.NewLine);
			stringBuilder.AppendJoin(Environment.NewLine, players.ConvertAll((Player player) => $"{player.Rating} — {player}"));

			string link = await Start.GetDeepLinkAsync(this.API, typeof(VoteResults));
			stringBuilder.Append(Environment.NewLine + Environment.NewLine + "Вы можете посмотреть обезличенные оценки себя по ссылке: " + link);

			return new TextMessage(stringBuilder.ToString()) { ParseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown }.NavigateToStart();
		}
	}
}