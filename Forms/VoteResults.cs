using System.Text;
using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrenburtisBot.Forms
{
	[BotCommand("Оценки игрока из формы")]
	internal class VoteResults : SqliteBotCommandFormBase
	{
		public static TextMessage Render(long userId)
		{
			using SqliteCommand command = new("SELECT MAX(id) FROM ratings_forms WHERE closed_timestamp IS NOT NULL", SqliteConnection);
			using SqliteDataReader reader = command.ExecuteReader();
			string formId = reader.HasRows && reader.Read() ? reader.GetString(0) : throw new NullReferenceException("Не удалось найти последнюю закрытую форму голосования за рейтинг");

			return Render(userId, formId);
		}

		public static TextMessage Render(long userId, string? formId)
		{
			using SqliteCommand timestampsCommand = new("SELECT opened_timestamp, closed_timestamp FROM ratings_forms WHERE ratings_forms.id = " + formId, SqliteConnection);
			using SqliteDataReader timestampsReader = timestampsCommand.ExecuteReader();
			if (!timestampsReader.HasRows || !timestampsReader.Read())
				throw new ArgumentException("Не удалось найти форму голосования за рейтинг с идентификатором " + formId, nameof(formId));

			DateTime opened = timestampsReader.GetDateTime(0);
			DateTime? closed = timestampsReader.IsDBNull(1) ? null : timestampsReader.GetDateTime(1);
			if (closed is null)
				throw new ArgumentException("Невозможно получить оценки из открытой формы голосования за рейтинг с идентификатором " + formId, nameof(formId));

			using SqliteCommand ratingsCommand = new("SELECT ratings_forms_users_votes.rating FROM users, ratings_forms_users, ratings_forms_users_votes"
				+ " WHERE ratings_forms_users_votes.form_user_id = ratings_forms_users.id AND ratings_forms_users.telegram_id = users.telegram_id"
				+ $" AND ratings_forms_users.ratings_form_id = {formId} AND ratings_forms_users_votes.telegram_id = {userId} ORDER BY ratings_forms_users_votes.rating DESC", SqliteConnection);

			using SqliteDataReader ratingsReader = ratingsCommand.ExecuteReader();

			int count = 0;
			double average = 0;
			Dictionary<int, int> marks = new(10);
			while (ratingsReader.HasRows && ratingsReader.Read())
			{
				int key = ratingsReader.GetInt32(0);
				marks.TryAdd(key, 0);

				marks[key] = marks[key] + 1;
				average += key;
				count++;
			}

			if (count == 0)
				throw new ArgumentException("Отсутствуют оценки игрока в форме голосования за рейтинг с идентификатором " + formId, nameof(userId));

			List<KeyValuePair<int, int>> sorted = [..marks];
			sorted.Sort((a, b) => b.Key.CompareTo(a.Key));

			StringBuilder stringBuilder = new StringBuilder().AppendJoin(", ", sorted.ConvertAll((pair) => $"{pair.Key} ({pair.Value})"));
			stringBuilder.AppendLine(Environment.NewLine + Environment.NewLine + "Количество оценок: " + count);
			stringBuilder.AppendLine("Среднее значение: " + Math.Round(average / count, 2));
			stringBuilder.AppendLine($"Голосование (ID {formId}) c {opened.ToString("dd.MM")} по {closed?.ToString("dd.MM")}");

			using SqliteCommand formsCommand = new("SELECT DISTINCT ratings_forms.id FROM ratings_forms, ratings_forms_users, ratings_forms_users_votes"
				+ " WHERE ratings_forms.id = ratings_forms_users.ratings_form_id AND ratings_forms_users.id = ratings_forms_users_votes.form_user_id"
				+ $" AND ratings_forms_users_votes.telegram_id = {userId} ORDER BY ratings_forms.id DESC", SqliteConnection);
			using SqliteDataReader formsReader = formsCommand.ExecuteReader();
			List<string> forms = [];
			while (formsReader.HasRows && formsReader.Read())
				if (formsReader.GetString(0) is string id && id != formId && !forms.Contains(id))
					forms.Add(id);

			ReplyKeyboardMarkup? replyKeyboard = forms.Count > 0 ? new ReplyKeyboardMarkup(true) { ResizeKeyboard = true } : null;
			const int VOTE_RESULTS_BUTTONS_MAX_COUNT = 6;
			for (int i = 0; replyKeyboard is not null && i < (forms.Count > VOTE_RESULTS_BUTTONS_MAX_COUNT ? VOTE_RESULTS_BUTTONS_MAX_COUNT : forms.Count); i++)
				replyKeyboard.AddButton(new(forms[i]));

			return new(stringBuilder.ToString()) { ReplyMarkup = replyKeyboard };
		}
	}
}