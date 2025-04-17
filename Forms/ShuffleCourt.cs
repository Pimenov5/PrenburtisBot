using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
	[BotCommand("Перемешать игроков в командах на площадке")]
	internal class ShuffleCourt : BotCommandFormBase
	{
		public TextMessage Render(long userId) => Render(userId, null);
		public TextMessage Render(long userId, string? courtId) => Render(userId, courtId, null);
		public TextMessage Render(long userId, string? courtId, string? messageIdToDelete)
		{
			Court court = Courts.GetById(ref courtId, userId);
			if (court.UserId != userId)
				throw new ArgumentException("Только создатель площадки может перемешивать игроков в командах", nameof(courtId));

			court.Shuffle();
			return new(string.Empty) { NavigateTo = new(new CourtPlayers(), courtId ?? throw new NullReferenceException(), messageIdToDelete, true) };
		}
	}
}