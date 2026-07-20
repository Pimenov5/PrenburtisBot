using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
using TelegramBotBase.Markdown;

namespace PrenburtisBot.Forms
{
	[BotCommand("Удалить посещения из абонемента", BotCommandScopeType.AllPrivateChats)]
	internal class LeaveSeason : SqliteBotCommandFormBase
	{
		private int? _seasonId = null;

		public async Task<TextMessage?> RenderAsync(long userId)
		{
			string dateFormat = (Environment.GetEnvironmentVariable("DB_DATE_FORMAT") ?? "yyyy-MM-dd") + " HH:mm:ss";
			using SqliteCommand datesCommand = new($"SELECT season_id, \"date\" FROM seasons_days WHERE season_id = (SELECT id FROM seasons WHERE \"{DateTime.UtcNow.ToString(dateFormat)}\" >= opened_timestamp "
				+ $"AND closed_timestamp IS NULL) AND telegram_id = {userId} ORDER BY \"date\"", SqliteConnection);
			using SqliteDataReader datesReader = datesCommand.ExecuteReader();
			List<DateOnly> dates = [];
			while (datesReader.Read())
			{
				_seasonId ??= datesReader.GetInt32(0);
				dates.Add(DateOnly.FromDateTime(datesReader.GetDateTime(1)));
			}

			if (dates.Count == 0)
				return new("Вы ещё не записались в текущий абонемент");

			await this.API.SendMessage(this.Device.DeviceId, $"В абонементе (ID {_seasonId}) вы записаны в следующие даты ({dates.Count}):" + Environment.NewLine
				+ string.Join(" ", dates.ConvertAll((DateOnly date) => date.Day)).Monospace(), Telegram.Bot.Types.Enums.ParseMode.Markdown);

			ConfirmDialog confirmDialog = new($"Удалить все даты посещений в абонементе (ID {_seasonId})?", new("Удалить", bool.TrueString), new("Отмена", bool.FalseString)) { AutoCloseOnClick = false };
			confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs eventArgs) =>
			{
				await confirmDialog.NavigateTo(this, eventArgs.Button.Value);
			};

			await this.NavigateTo(confirmDialog);
			return null;
		}

		public TextMessage Render(long userId, string strIsConfirmed)
		{
			if (!bool.Parse(strIsConfirmed))
				return new TextMessage(string.Empty).NavigateToStart();

			int count = default;
			using SqliteTransaction transaction = SqliteConnection.BeginTransaction();
			try
			{
				using SqliteCommand deleteCommand = new($"DELETE FROM seasons_days WHERE telegram_id = {userId} AND season_id = {_seasonId}", SqliteConnection, transaction);
				using SqliteDataReader deleteReader = deleteCommand.ExecuteReader();
				count = deleteReader.RecordsAffected;
				if (count == 0)
					throw new ArgumentException($"Не удалось удалить выбранные пользователем (ID {userId}) дни тренировок в абонементе с ID {_seasonId}");

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}

			return new TextMessage($"Количество удалённых из абонемента (ID {_seasonId}) дат: {count}").NavigateToStart();
		}
	}
}