using Microsoft.Data.Sqlite;
using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Уведомить неголосовавших", "BOT_OWNER_CHAT_ID")]
	internal class NotifyVoters : SqliteBotCommandFormBase
	{
		public async Task<TextMessage> RenderAsync()
		{
			using SqliteCommand formCommand = new("SELECT MAX(id) FROM ratings_forms WHERE closed_timestamp IS NULL", SqliteConnection);
			long formId = (long)(formCommand.ExecuteScalar() ?? throw new Exception("Не удалось найти последнюю незакрытую форму для голосования за рейтинг"));

			using SqliteCommand idsCommand = new(string.Format("SELECT telegram_id FROM ratings_forms_permissions WHERE can_vote = 1 AND ratings_form_id = {0}"
				+ " AND telegram_id NOT IN (SELECT telegram_id FROM ratings_forms_users WHERE ratings_form_id = {0})", formId), SqliteConnection);
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
				if (bool.Parse(args.Button.Value))
				{
					string text = "Напоминаю, сегодня последний день голосования за обновлённый рейтинг игроков! Чтобы начать, нажмите на команду /" + typeof(VoteRatings).Name.ToLower();
					List<Message> messages = new(players.Count);
					foreach (Player player in players)
					{
						try
						{
							Message message = await this.API.SendMessage(player.UserId, text);
							messages.Add(message);
						}
						catch (Exception ex)
						{
							Console.Error.WriteLine(ex.ToString());
						}
					}

					stringBuilder.Clear();
					stringBuilder.Append($"Уведомления отправлены в чаты ({messages.Count}): ").AppendJoin(", ", messages.ConvertAll((message) => message.Chat.FirstName));

					await confirmDialog.Device.Send(stringBuilder.ToString());
				}

				await confirmDialog.NavigateTo(new Start());
			};

			return new(string.Empty) { NavigateTo = new(confirmDialog) };
		}
	}
}