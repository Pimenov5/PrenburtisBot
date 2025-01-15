namespace PrenburtisBot.Types
{
	internal class Court
	{
		private uint _teamMaxPlayerCount;
		private readonly List<Team> _teams;
		private readonly HashSet<long> _leavedUsers = [];

		private static Exception? CanCreate(uint teamCount, uint teamMaxPlayerCount, int playerCount = default)
		{
			if (teamCount < 2)
				return new ArgumentException("Количество команд не может быть меньше двух", nameof(teamCount));
			if (teamMaxPlayerCount < 2)
				return new ArgumentException("Количество игроков в команде не может быть меньше двух", nameof(teamMaxPlayerCount));
			if (playerCount != default && teamCount * teamMaxPlayerCount is uint capacity && playerCount > capacity)
				return new InvalidOperationException($"Количество игроков на площадке ({playerCount}) не может превышать её вместимость ({capacity}):"
					+ Environment.NewLine + $"{teamCount} (количество команд) * {teamMaxPlayerCount} (количество игроков в команде)");
			return null;
		}

		protected virtual List<Team> GetTeams(Player player)
		{
			if (_leavedUsers.Contains(player.UserId))
				throw new InvalidOperationException("Нельзя снова присоединиться к площадке после выхода из неё");

			List<Team> teams = new(this.Teams.Where((Team team) => team.PlayerCount < this.TeamMaxPlayerCount));
			if (teams.Any((Team team) => team.PlayerCount == 0))
				teams.RemoveAll((Team team) => team.PlayerCount > 0);

			return teams;
		}

		public readonly long UserId;
		public uint TeamMaxPlayerCount => _teamMaxPlayerCount;
		public Team[] Teams => _teams.ToArray();
		public int TeamCount => _teams.Count;

		public Court(long userId, List<Team> teams, uint teamMaxPlayerCount)
		{
			if (CanCreate((uint)teams.Count, teamMaxPlayerCount) is Exception exception)
				throw exception;
			foreach (Team team in teams)
				if (team.PlayerCount > teamMaxPlayerCount)
					throw new ArgumentOutOfRangeException(nameof(teamMaxPlayerCount), $"В команде #{1 + teams.IndexOf(team)} превышено максимальное количество игроков");

			this.UserId = userId;
			_teamMaxPlayerCount = teamMaxPlayerCount;
			_teams = teams;
		}

		public uint? AddPlayer(Player player, Random? random = null)
		{
			List<Team> teams = this.GetTeams(player);
			if (teams.Count == 0)
				return null;

			Team team = teams[teams.Count == 1 ? 0 : (random ?? new Random()).Next(teams.Count)];
			team.AddPlayer(player);
			return (uint)_teams.IndexOf(team);
		}

		public virtual uint?[] AddPlayers(IEnumerable<Player> collection)
		{
			Random random = new();
			List<Player> players = new(collection);
			uint?[] result = new uint?[players.Count];

			for (int i = 0; i < players.Count; i++)
			{
				int index = random.Next(players.Count);
				result[i] = this.AddPlayer(players[index], random);
				players.RemoveAt(index);
			}

			return result;
		}

		public int[] RemovePlayer(long userId, bool userLeaved = true)
		{
			int[] result = new int[_teams.Count];
			int index = 0;
			for (int i = 0; i < _teams.Count; i++)
				if (_teams[i].RemovePlayer(userId) > 0)
					result[index++] = i;

			Array.Resize(ref result, index);
			if (userLeaved && result.Length > 0)
				_leavedUsers.Add(userId);

			return result;
		}

		public bool ContainsPlayer(long userId) => _teams.Any((Team team) => team.Contains(userId));

		public void Edit(uint teamCount, uint teamMaxPlayerCount)
		{
			_teamMaxPlayerCount = teamMaxPlayerCount;
			int count = 0;
			foreach (Team team in this.Teams)
				count += team.PlayerCount;

			if (CanCreate(teamCount, teamMaxPlayerCount, count) is Exception exception)
				throw exception;

			List<Player> players = [];
			int prevTeamCount = _teams.Count;
			if (teamCount > prevTeamCount)
			{
				List<string> usedNames = [];
				foreach (Team team in this.Teams)
					if (!string.IsNullOrEmpty(team.Name))
						usedNames.Add(team.Name);

				List<string> names = usedNames.Count == 0 ? [] : new(Team.Names);
				foreach (string name in usedNames)
					names.Remove(name);

				Random? random = names.Count >= teamCount - prevTeamCount ? new() : null;
				for (int i = 0; i < teamCount - prevTeamCount; i++)
					_teams.Add(new Team(random is null ? null : names[random.Next(names.Count)]));
			}
			else if (teamCount < prevTeamCount)
			{
				while (_teams.Count > teamCount)
				{
					players.AddRange(_teams[^1].Players);
					_teams.RemoveAt(_teams.Count - 1);
				}
			}

			foreach (Team team in this.Teams)
				if (team.PlayerCount > teamMaxPlayerCount)
				{
					int index = (int)teamMaxPlayerCount;
					count = team.PlayerCount - index;
					players.AddRange(team.Players.ToList().GetRange(index, count));
					team.RemovePlayers(index, count);
				}

			if (players.Count > 0)
				this.AddPlayers(players);
		}

		public bool Shuffle()
		{
			List<Player> players = [];
			foreach (Team team in this.Teams)
				if (team.PlayerCount > 0)
				{
					players.AddRange(team.Players);
					team.RemovePlayers(0, team.PlayerCount);
				}

			if (players.Count == 0)
				return false;

			this.AddPlayers(players);
			return true;
		}
	}
}