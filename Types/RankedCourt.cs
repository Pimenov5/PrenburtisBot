namespace PrenburtisBot.Types
{
	internal class RankedCourt(long userId, List<Team> teams, uint teamMaxPlayerCount) : Court(userId, teams, teamMaxPlayerCount)
	{
		private bool _mustSort = true;
		private Gender? _genderMinority;
		private delegate IComparable GetComparableInterface(Team team);

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

			void SortAndRemoveTeams(GetComparableInterface func)
			{
				if (teams.Count <= 1)
					return;

				teams.Sort((Team x, Team b) => func(x).CompareTo(func(b)));
				teams.RemoveAll((Team team) => !func(team).Equals(func(teams.First())));
			}

			SortAndRemoveTeams((Team team) => team.PlayerCount);
			if (_mustSort)
				SortAndRemoveTeams((Team team) => team.GetRankCount(player.Rank));
			else
			{
				if (_genderMinority is Gender genderMinority && player.Gender == genderMinority)
					SortAndRemoveTeams((Team team) => team.GetGenderCount(player.Gender));
				SortAndRemoveTeams((Team team) => team.RatingSum);
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
				int femaleCount = players.Count((Player player) => player.Gender == Gender.Female);
				int maleCount = players.Count - femaleCount;
				_genderMinority = maleCount == femaleCount ? null : maleCount > femaleCount ? Gender.Female : Gender.Male;

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
					_genderMinority = null;
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