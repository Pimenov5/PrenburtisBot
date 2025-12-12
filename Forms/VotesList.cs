using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Список проголосовавших", "BOT_OWNER_CHAT_ID")]
	internal class VotesList : SqliteBotCommandFormBase
	{
		public async Task<TextMessage> RenderAsync()
		{
			using SqliteCommand command = new("SELECT MAX(id) FROM ratings_forms WHERE closed_timestamp", SqliteConnection);
			using SqliteDataReader reader = command.ExecuteReader();
			string formId = reader.HasRows && reader.Read() ? reader.GetString(0) : throw new NullReferenceException("Не удалось найти последнюю форму голосования за рейтинг");

			return await RenderAsync(formId);
		}

		public async Task<TextMessage> RenderAsync(string formId) => await RenderAsync(formId, null);
		public async Task<TextMessage> RenderAsync(string formId, string? messageIdToDelete)
		{
			using SqliteCommand idsCommand = new($"SELECT telegram_id, timestamp FROM ratings_forms_users WHERE ratings_forms_users.ratings_form_id = {formId} ORDER BY timestamp", SqliteConnection);
			using SqliteDataReader idsReader = idsCommand.ExecuteReader();

			List<KeyValuePair<long, DateTime>> ids = [];
			while (idsReader.HasRows && idsReader.Read())
				ids.Add(new(idsReader.GetInt64(0), idsReader.GetDateTime(1)));

			DateOnly? dateOnly = null;
			StringBuilder stringBuilder = new("Количество проголосовавших: " + ids.Count.ToString());
			for (int i = 0; i < ids.Count; i++)
			{
				if (dateOnly is null || dateOnly != DateOnly.FromDateTime(ids[i].Value))
				{
					dateOnly = DateOnly.FromDateTime(ids[i].Value);
					stringBuilder.AppendLine();
					stringBuilder.Append($"{dateOnly?.ToString("dd.MM")} ({ids.Count((pair) => DateOnly.FromDateTime(pair.Value) == dateOnly)}): ");
				}
				else
					stringBuilder.Append(", ");

				stringBuilder.Append(Users.GetPlayer(ids[i].Key, ids[i].Key.ToString(), null, false));
			}

			InlineKeyboardMarkup? inlineKeyboard = null;
			if (Environment.GetEnvironmentVariable("MESSAGE_ID_ALIAS") is string messageIdAlias && !string.IsNullOrEmpty(messageIdAlias)) 
				inlineKeyboard = new(new InlineKeyboardButton("🔄") { CallbackData = new CallbackData(nameof(VotesList), Commands.ParamsToString(formId, messageIdAlias)).Serialize() });

			if (!string.IsNullOrEmpty(messageIdToDelete) && int.TryParse(messageIdToDelete, out int messageId))
				await this.Device.DeleteMessage(messageId);

			return new(stringBuilder.ToString()) { ParseMode = ParseMode.Markdown, ReplyMarkup = inlineKeyboard };
		}
	}
}