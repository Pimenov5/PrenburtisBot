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

		public override uint?[] AddPlayers(IEnumerable<Player> collection)
		{
			List<Player> players = new(collection);
			players.Sort((Player x, Player y) => x.Rank.CompareTo(y.Rank));
			uint?[] result = new uint?[players.Count];

			int i = -1;
			Random random = new();
			Dictionary<uint, List<Player>> teams = [];
			foreach (Player player in players)
			{
				result[++i] = this.AddPlayer(player, random);
				if (result[i] is uint index)
				{
					teams.TryAdd(index, []);
					teams[index].Add(player);
				}
			}

			foreach (uint index in teams.Keys)
				this.Teams[index].Shuffle(teams[index].ToArray(), random);

			return result;
        }
	}
}