namespace PrenburtisBot.Types
{
	internal class Team(string? name = null)
	{
		private readonly List<Player> _players = [];

		public string? Name = name;
		public int PlayerCount => _players.Count;
		public IEnumerable<Player> Players => _players;

		public void AddPlayer(Player player) => _players.Add(player);
		public int RemovePlayer(long userId) => _players.RemoveAll((Player player) => player.UserId == userId);
		public void RemovePlayers(int index, int count) => _players.RemoveRange(index, count);
		public int GetRankCount(int rank) => _players.Count((Player player) => player.Rank == rank);
		public bool Contains(long userId) => _players.Any((Player player) => player.UserId == userId);

		public string FormatName(string format = " {0}") => string.IsNullOrEmpty(this.Name) ? string.Empty : string.Format(format, this.Name);
	}
}