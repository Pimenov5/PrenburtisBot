using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
	[BotCommand("Перемешать игроков в командах на площадке")]
	internal class ShuffleCourt : BotCommandFormBase
	{
		protected override async Task<TextMessage?> RenderAsync(long userId, string[] args)
		{
			string? courtId = args.Length == 1 ? args[0] : null;
			Court court = Courts.GetById(ref courtId, userId);
			if (court.UserId != userId)
				return new("Только создатель площадки может перемешивать игроков в командах");

			court.Shuffle();
			await this.NavigateTo(new CourtPlayers(), courtId);
			return null;
		}
	}
}