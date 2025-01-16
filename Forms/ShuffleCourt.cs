using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
	[BotCommand("Перемешать игроков в командах на площадке")]
	internal class ShuffleCourt : BotCommandFormBase
	{
		public TextMessage Render(long userId) => Render(userId, null);
		public TextMessage Render(long userId, string? courtId)
		{
			Court court = Courts.GetById(ref courtId, userId);
			if (court.UserId != userId)
				return new("Только создатель площадки может перемешивать игроков в командах");

			court.Shuffle();
			return new(string.Empty) { NavigateTo = new(new CourtPlayers(), courtId) };
		}
	}
}