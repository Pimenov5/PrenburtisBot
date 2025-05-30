using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Список игроков на площадке")]
	internal class CourtPlayers : BotCommandFormBase
	{
		public static string ToString(ref string? courtId, long userId, bool? isGroup) => ToString(Courts.GetById(ref courtId, userId), userId, isGroup);
		public static string ToString(Court court, long userId, bool? isGroup)
		{
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

			StringBuilder stringBuilder = new(count == 0 ? "Нет игроков на площадке"
				: $"{count} из {maxCount} игроков на площадке{((isGroup ?? false) && Courts.IndexOf(court) is int index && index >= 0 ? $" с ID {index}" : string.Empty)}"
				+ Environment.NewLine);

			int i = default;
			foreach (Team team in teams)
			{
				double? rating = null;
				if (court is RankedCourt)
					foreach (Player player in team.Players)
						rating = rating is null ? player.Rating : rating + player.Rating;

				string teamName = team.FormatName();
				string value = $"{(string.IsNullOrEmpty(teamName) ? "Команда " : string.Empty)}#{++i}{teamName} ({team.PlayerCount}{(rating is null ? string.Empty : $" = {Math.Round((double)rating, 1)}")}): ";
				if (isGroup is bool boolValue && !boolValue && team.Contains(userId))
					value = (string.IsNullOrEmpty(teamName) ? "Ваша" : "Вы в" + ' ' +  value).ToUpper();

				stringBuilder.Append(value);
				stringBuilder.AppendJoin(", ", team.Players);
				stringBuilder.AppendLine();
			}

			return stringBuilder.ToString();
		}

		public async Task<TextMessage> RenderAsync(long userId) => await RenderAsync(userId, null, null);
		public async Task<TextMessage> RenderAsync(long userId, string? courtId) => await RenderAsync(userId, courtId, null);
		public async Task<TextMessage> RenderAsync(long userId, string? courtId, string? messageIdToDelete) => await RenderAsync(userId, courtId, messageIdToDelete, null);
		public async Task<TextMessage> RenderAsync(long userId, string? courtId, string? messageIdToDelete, string? needShuffleButton)
		{
			string text = CourtPlayers.ToString(ref courtId, userId, this.Device.IsGroup);

			ButtonForm? buttonForm = null;
			if (text.Contains('#') && !string.IsNullOrEmpty(courtId) && Environment.GetEnvironmentVariable("MESSAGE_ID_ALIAS") is string messageIdAlias)
			{
				buttonForm = new();
				if (bool.TryParse(needShuffleButton, out bool addShuffleButton) && addShuffleButton)
					buttonForm.AddButtonRow(new ButtonBase("🔀", new CallbackData(nameof(ShuffleCourt), Commands.ParamsToString(courtId, messageIdAlias)).Serialize()));
				else
					buttonForm.AddButtonRow(new ButtonBase("🔄", new CallbackData(nameof(CourtPlayers), Commands.ParamsToString(courtId, messageIdAlias)).Serialize()));
			}

			if (!string.IsNullOrEmpty(messageIdToDelete) && int.TryParse(messageIdToDelete, out int messageId))
				await this.Device.DeleteMessage(messageId);

			return new TextMessage(text) { Buttons = buttonForm, ParseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown }.NavigateToStart(Start.SET_QUIET);
		}
	}
}