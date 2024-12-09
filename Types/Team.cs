namespace PrenburtisBot.Types
{
	internal class Team(string? name = null)
	{
		private static readonly List<string> _names = [];
		private readonly List<Player> _players = [];

		public string? Name = name;
		public int PlayerCount => _players.Count;
		public IEnumerable<Player> Players => _players;
		public static string[] Names => _names.ToArray();

		public void AddPlayer(Player player) => _players.Add(player);
		public int RemovePlayer(long userId) => _players.RemoveAll((Player player) => player.UserId == userId);
		public void RemovePlayers(int index, int count) => _players.RemoveRange(index, count);
		public int GetRankCount(int rank) => _players.Count((Player player) => player.Rank == rank);
		public bool Contains(long userId) => _players.Any((Player player) => player.UserId == userId);
		public uint Shuffle(Player[] players, Random? random = null)
		{
			uint result = 0;
			random ??= new Random();
			foreach (Player player in players)
				if (_players.IndexOf(player) is int index && index >= 0)
				{
					int count = 0;
					int newIndex = random.Next(this.PlayerCount);
					while (count < players.Length)
					{
						if (players.Contains(_players[newIndex]))
						{
							Player temp = _players[newIndex];
							_players[newIndex] = player;
							_players[index] = temp;
							result++;
							break;
						}
						else
							count++;
					}
				}

			return result;
		}

		public string FormatName(string format = " {0}") => string.IsNullOrEmpty(this.Name) ? string.Empty : string.Format(format, this.Name);
		public static uint ReadNames(TextReader reader)
		{
			int count = _names.Count;
			while (reader.ReadLine() is string name)
				_names.Add(name);

			return (uint)(_names.Count - count);
		}
	}
}