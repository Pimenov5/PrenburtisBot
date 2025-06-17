using System.Text;

namespace PrenburtisBot.Types
{
	internal class Team(string? name = null)
	{
		private const char INDEXES_SEPARATOR = ',';
		private class ReverseIntegerComparer : IComparer<int>
		{
			public int Compare(int x, int y) => Comparer<int>.Default.Compare(y, x);
		}

		private static int? s_prevNameCount;
		private static readonly SortedSet<int> s_nameIndexes = new(new ReverseIntegerComparer());
		private static readonly List<string> _names = [];
		private readonly List<Player> _players = [];

		public string? Name = name;
		public int PlayerCount => _players.Count;
		public IEnumerable<Player> Players => _players;
		public static IReadOnlyList<string> Names { 
			get
			{
				List<string> names = [.._names];
				foreach (int index in s_nameIndexes)
					names.RemoveAt(index);

				return names;
			} }

		public void AddPlayer(Player player) => _players.Add(player);
		public int RemovePlayer(long userId) => _players.RemoveAll((Player player) => player.UserId == userId);
		public void RemovePlayers(int index, int count) => _players.RemoveRange(index, count);
		public int GetRankCount(int rank) => _players.Count((Player player) => player.Rank == rank);
		public int GetGenderCount(Gender gender) => _players.Count((Player player) => player.Gender == gender);
		public double RatingSum => _players.Sum((Player player) => player.Rating);
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
		public static int ReadNames(TextReader reader)
		{
			List<string> names = [];
			while (reader.ReadLine()?.Trim() is string name && !string.IsNullOrEmpty(name))
				names.Add(name);

			if (s_nameIndexes.Count == 0 && Session.Get(typeof(Team), nameof(s_nameIndexes)) is string strValue 
				&& strValue.Split(INDEXES_SEPARATOR, StringSplitOptions.RemoveEmptyEntries) is string[] strArray && strArray.Length > 0)
			{
				foreach (string item in strArray)
					if (int.TryParse(item, out int index) && index >= 0)
						s_nameIndexes.Add(index);
					else
					{
						s_nameIndexes.Clear();
						throw new InvalidCastException($"\"{item}\" не является индексом имени команды");
					}
			}

			if (_names.Count == 0)
			{
				s_prevNameCount ??= Session.Get(typeof(Team), nameof(s_prevNameCount)) is string strCount && int.TryParse(strCount, out int count) ? count : null;
				if (s_prevNameCount != names.Count)
					s_nameIndexes.Clear();
			}
			else
			{
				List<string> smallestList = names.Count <= _names.Count ? names : _names, biggestList = names.Count > _names.Count ? names : _names;
				for (int i = 0; i < smallestList.Count; i++)
					if (smallestList[i] != biggestList[i])
					{
						s_nameIndexes.Clear();
						break;
					}
			}

			_names.Clear();
			_names.AddRange(names);

			s_prevNameCount = names.Count;
			Session.Set(typeof(Team), nameof(s_prevNameCount), s_prevNameCount.ToString() ?? throw new NullReferenceException(nameof(s_prevNameCount)));
			Session.Set(typeof(Team), nameof(s_nameIndexes), new StringBuilder().AppendJoin(INDEXES_SEPARATOR, s_nameIndexes).ToString());
			Session.TryWrite();

			return names.Count;
		}

		public static void WriteSession(IEnumerable<Team> teams)
		{
			if (_names.Count == 0)
				return;

			int count = s_nameIndexes.Count;
			foreach (Team team in teams)
				if (team.Name is string name && _names.IndexOf(name) is int index && index >= 0)
					s_nameIndexes.Add(index);

			if (count == s_nameIndexes.Count)
				return;

			Session.Set(typeof(Team), nameof(s_nameIndexes), new StringBuilder().AppendJoin(INDEXES_SEPARATOR, s_nameIndexes).ToString());
			Session.TryWrite();
		}
	}
}