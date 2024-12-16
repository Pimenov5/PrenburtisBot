using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;

namespace PrenburtisBot.Forms
{
	[BotCommand("Редактировать площадку")]
	internal class Edit : Teams
	{
		private string? _courtId = null;
		private Court? _court = null;

		protected override async Task<TextMessage?> RenderAsync(long userId, params string[] args)
		{
			_courtId = _court is not null ? _courtId : args.Length > 0 ? args[0] : null;
			_court ??= Courts.GetById(ref _courtId, userId);

			if (_court.UserId != userId)
				return new("Редактировать площадку может только её создатель");

			if (!await this.CanCreateCourtAsync(args.Length > 0 && args[0] == _courtId ? args[1..] : args))
				return null;

			HashSet<string> links = [];
			while (_court is not RankedCourt && (_isRanked ?? throw new NullReferenceException()))
			{
				foreach (Team team in _court.Teams)
					foreach (Player player in team.Players)
						if (player.Rank != default)
						{
							int count = 0;
							foreach (Team tempTeam in _court.Teams)
								foreach (Player tempPlayer in tempTeam.Players)
									if (tempPlayer.UserId == player.UserId)
										++count;

							if (count > 1)
								links.Add(player.Link);
						}

				if (links.Count == 0)
					break;

				await this.Device.Send($"Невозможно включить учёт рангов на площадке, т.к. {(links.Count == 1 ? $"ранговый игрок {links.First()} присоединился" 
					: $"ранговые игроки ({new StringBuilder().AppendJoin(", ", links)}) присоединялись")} к ней больше одного раза");

				_isRanked = null;
				if (await this.CanCreateCourtAsync([]))
					links.Clear();
				else
					return null;
			}

			Court newCourt = _isRanked ?? throw new NullReferenceException() ? new RankedCourt(this.Device.DeviceId, _court.Teams.ToList(), _court.TeamMaxPlayerCount)
				: new Court(this.Device.DeviceId, _court.Teams.ToList(), _court.TeamMaxPlayerCount);

			newCourt.Edit(_teamCount, _teamMaxPlayerCount);
			Courts.Replace(int.Parse(_courtId ?? throw new NullReferenceException()), newCourt);

			await this.NavigateTo(new Players(), _courtId);
			return null;
		}
	}
}