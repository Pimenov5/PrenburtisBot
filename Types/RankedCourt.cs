namespace PrenburtisBot.Types
{
	internal class RankedCourt(long userId, List<Team> teams, uint teamMaxPlayerCount) : Court(userId, teams, teamMaxPlayerCount)
	{
		protected override List<Team> GetTeams(Player player)
		{
			if (player.Rank != default)
			{
				int number = 0;
				foreach (Team team in this.Teams)
				{
					++number;
					foreach (Player item in team.Players)
						if (item.UserId == player.UserId)
							throw new InvalidOperationException($"Вы уже были добавлены в команду #{number}" + Environment.NewLine
								+ "Нельзя повторно присоединяться к площадке, на которой учитываются ранги игроков");
				}
			}

			List<Team> teams = base.GetTeams(player);

			teams.Sort((Team a, Team b) => a.GetRankCount(player.Rank).CompareTo(b.GetRankCount(player.Rank)));
			teams.RemoveAll((Team team) => team.GetRankCount(player.Rank) != teams.First().GetRankCount(player.Rank));

			teams.Sort((Team a, Team b) => a.PlayerCount.CompareTo(b.PlayerCount));
			teams.RemoveAll((Team team) => team.PlayerCount != teams.First().PlayerCount);

			return teams;
		}
	}
}