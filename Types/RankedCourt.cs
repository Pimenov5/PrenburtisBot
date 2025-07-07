namespace PrenburtisBot.Types
{
	internal class RankedCourt(long userId, List<Team> teams, uint teamMaxPlayerCount) : Court(userId, teams, teamMaxPlayerCount)
	{
		private bool _mustSortByRank = true, _isLastPlayers = false;
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

			void SortAndRemoveTeams(GetComparableInterface func, bool ascendingSortOrder = true)
			{
				if (teams.Count <= 1)
					return;

				teams.Sort((Team x, Team y) => func(x).CompareTo(func(y)) * (ascendingSortOrder ? 1 : -1));
				teams.RemoveAll((Team team) => !func(team).Equals(func(teams.First())));
			}

			SortAndRemoveTeams((Team team) => team.PlayerCount);
			if (_genderMinority is Gender genderMinority && player.Gender == genderMinority)
				SortAndRemoveTeams((Team team) => team.GetGenderCount(player.Gender));

			if (_mustSortByRank)
				SortAndRemoveTeams((Team team) => team.GetRankCount(player.Rank), !_isLastPlayers);
			else
				SortAndRemoveTeams((Team team) => team.RatingSum, !_isLastPlayers);

			return teams;
		}

		public override uint?[] AddPlayers(IEnumerable<Player> collection)
		{
			Random random = new();
			int digits = int.TryParse(Environment.GetEnvironmentVariable("ROUND_RATING_TO_DIGITS"), out digits) ? digits : 2;

			List<Player> players = [..collection];
			players.Sort((Player x, Player y) => Math.Round(y.Rating, digits) == Math.Round(x.Rating, digits) ? random.Next(-1, 1) : y.Rating.CompareTo(x.Rating));
			List<uint?> result = new(players.Count);

			_mustSortByRank = false;
			foreach (Team team in this.Teams)
				if (team.PlayerCount != 0)
				{
					_mustSortByRank = true;
					break;
				}

			if (_mustSortByRank)
			{
				foreach (Player player in players)
					result.Add(this.AddPlayer(player, random));
			}
			else
			{
				int femaleCount = players.Count((Player player) => player.Gender == Gender.Female);
				int maleCount = players.Count - femaleCount;
				_genderMinority = maleCount == femaleCount ? null : maleCount > femaleCount ? Gender.Female : Gender.Male;

				while (players.Count > 0)
				{
					int count = this.TeamCount < players.Count ? this.TeamCount : players.Count;
					List<Player> range = players.GetRange(0, count);
					_isLastPlayers = count < this.TeamCount;
					foreach (Player player in range)
						result.Add(this.AddPlayer(player, random));

					players.RemoveRange(0, count);
				}
			}

			return [..result];
        }
	}
}