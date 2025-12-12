using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Уведомить неголосовавших", "BOT_OWNER_CHAT_ID")]
	internal class NotifyVoters : SqliteBotCommandFormBase
	{
		private List<Player>? _players;

		public async Task<TextMessage> RenderAsync()
		{
			using SqliteCommand formCommand = new("SELECT MAX(id) FROM ratings_forms WHERE closed_timestamp IS NULL", SqliteConnection);
			int formId = (int)(formCommand.ExecuteScalar() ?? throw new Exception("Не удалось найти последнюю незакрытую форму для голосования за рейтинг"));

			using SqliteCommand idsCommand = new("SELECT telegram_id FROM users WHERE need_vote = true AND telegram_id NOT IN"
				+ $" (SELECT telegram_id FROM ratings_forms_users WHERE ratings_form_id = {formId})", SqliteConnection);
			using SqliteDataReader idsReader = idsCommand.ExecuteReader();

			List<long> ids = [];
			while (idsReader.Read())
				ids.Add(idsReader.GetInt64(0));

			if (ids.Count == 0)
				return new TextMessage($"Все пользователи проголосовали за рейтинг в форме ({formId})").NavigateToStart(Start.SET_QUIET);

			List<Player> players = [..Users.GetPlayers()];
			players.RemoveAll((Player player) => !ids.Contains(player.UserId));

			StringBuilder stringBuilder = new StringBuilder($"Не проголосовавшие за рейтинг в форме ({formId}) пользователи: ").AppendJoin(", ", players);
			await this.API.SendMessage(this.Device.DeviceId, stringBuilder.ToString(), parseMode: ParseMode.Markdown);

			ConfirmDialog confirmDialog = new("Отправить уведомление этим пользователям?", new("Отправить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs args) =>
			{
				_players = players;
				await confirmDialog.NavigateTo(this, args.Button.Value);
			};

			return new(string.Empty) { NavigateTo = new(confirmDialog) };
		}
		/*
		public async Task<TextMessage> RenderAsync(string strIsConfirmed)
		{
			if (_players is null)
				throw new NullReferenceException("")
		}*/
	}
}