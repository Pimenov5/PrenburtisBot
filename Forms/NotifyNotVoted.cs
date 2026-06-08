using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot.Forms
{
	[BotCommand("Уведомить не проголосовавших в опросе", BotCommandScopeType.AllChatAdministrators)]
	internal class NotifyNotVoted : RepliedToPollGroupFormBase
	{
		protected override string? GetFirstPollOption() => null;

		protected virtual string GetText(IReadOnlyCollection<Player> players, List<Player> notVoted, params string[] args)
		{
			StringBuilder stringBuilder = new();
			stringBuilder.AppendJoin(' ', args).AppendLine(Environment.NewLine);
			stringBuilder.AppendJoin(", ", notVoted);

			return stringBuilder.ToString();
		}

		protected override TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args)
		{
			List<Player> notVoted = [..Users.GetPlayers()];
			notVoted.RemoveAll((Player player) => player is not User user || user.IsArchived || players.Any((player) => player.UserId == user.UserId));
			if (notVoted.Count == 0)
				throw new ArgumentException("В БД нет активных игроков, которые не проголосовали в опросе", nameof(players));

			string text = this.GetText(players, notVoted, args);
			return new TextMessage(text) { ParseMode = ParseMode.Markdown };
		}
	}
}