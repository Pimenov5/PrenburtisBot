using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Список игроков на площадке")]
	internal class CourtPlayers : BotCommandFormBase
	{
		public async Task<TextMessage> RenderAsync(long userId) => await RenderAsync(userId, null, null);
		public async Task<TextMessage> RenderAsync(long userId, string? courtId) => await RenderAsync(userId, courtId, null);
		public async Task<TextMessage> RenderAsync(long userId, string? courtId, string? messageIdToDelete)
		{
			Court court = Courts.GetById(ref courtId, userId);

			Team[] teams = court.Teams;
			int count = 0, maxCount = 0;
			bool canSeePlayers = false;
			foreach (Team team in teams)
			{
				count += team.PlayerCount;
				maxCount += (int)court.TeamMaxPlayerCount;
				canSeePlayers = canSeePlayers ? canSeePlayers : team.Contains(userId);
			}

			canSeePlayers = canSeePlayers ? canSeePlayers : userId == court?.UserId;
			if (!canSeePlayers)
				return new("Просматривать игроков на площадке могут только присоединившиеся и её создатель");
			else if (count == 0)
				teams = [];

			StringBuilder stringBuilder = new(count == 0 ? "Нет игроков на площадке" : $"Игроков на площадке: {count} из {maxCount}" + Environment.NewLine);
			int i = default;
			foreach (Team team in teams)
			{
				string value = $"Команда #{++i}{team.FormatName()} ({team.PlayerCount}): ";
				if (!this.Device.IsGroup && team.Contains(userId))
					value = ("Ваша " + value).ToUpper();

				stringBuilder.Append(value);
				stringBuilder.AppendJoin(", ", team.Players);
				stringBuilder.AppendLine();
			}

			ButtonForm? buttonForm = null;
			if (i != default && !string.IsNullOrEmpty(courtId) && Environment.GetEnvironmentVariable("MESSAGE_ID_ALIAS") is string messageIdAlias)
			{
				buttonForm = new();
				buttonForm.AddButtonRow(new ButtonBase("🔄", new CallbackData(nameof(CourtPlayers), Commands.ParamsToString(courtId, messageIdAlias)).Serialize()));
			}

			if (!string.IsNullOrEmpty(messageIdToDelete) && int.TryParse(messageIdToDelete, out int messageId))
				await this.Device.DeleteMessage(messageId);

			return new TextMessage(stringBuilder.ToString()) { Buttons = buttonForm, ParseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET);
		}
	}
}