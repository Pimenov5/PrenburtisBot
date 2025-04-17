namespace PrenburtisBot.Types
{
	internal class RankedCourt(long userId, List<Team> teams, uint teamMaxPlayerCount) : Court(userId, teams, teamMaxPlayerCount)
	{
		private bool _mustSort = true;

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

			if (teams.Count > 1)
			{
				if (_mustSort)
				{
					teams.Sort((Team a, Team b) => a.GetRankCount(player.Rank).CompareTo(b.GetRankCount(player.Rank)));
					teams.RemoveAll((Team team) => team.GetRankCount(player.Rank) != teams.First().GetRankCount(player.Rank));
				}

				teams.Sort((Team a, Team b) => a.PlayerCount.CompareTo(b.PlayerCount));
				teams.RemoveAll((Team team) => team.PlayerCount != teams.First().PlayerCount);
			}

			return teams;
		}

		public override uint?[] AddPlayers(IEnumerable<Player> collection)
		{
			List<Player> players = new(collection);
			players.Sort((Player x, Player y) => y.Rating.CompareTo(x.Rating));
			List<uint?> result = new (players.Count);

			Random random = new();

			bool isEmpty = true;
			foreach (Team team in this.Teams)
				if (team.PlayerCount != 0)
				{
					isEmpty = false;
					break;
				}

			if (isEmpty)
			{
				_mustSort = false;
				try
				{
					while (players.Count > 0)
					{
						int count = this.TeamCount < players.Count ? this.TeamCount : players.Count;
						List<Player> range = players.GetRange(0, count);
						foreach (Player player in range)
							result.Add(this.AddPlayer(player, random));

						players.RemoveRange(0, count);
					}
				}
				finally
				{
					_mustSort = true;
				}
			}
			else
			{
				foreach (Player player in players)
					result.Add(this.AddPlayer(player, random));
			}

			return result.ToArray();
        }
	}
}