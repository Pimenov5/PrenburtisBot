namespace PrenburtisBot.Types
{
	internal static class Courts
	{
		private static readonly List<Court> _courts = [];

		public static int Add(Court court)
		{
			_courts.Add(court);
			return _courts.Count - 1;
		}

		public static bool Contains(int index) => 0 <= index && index < _courts.Count;
		public static void Replace(int index, Court court) => _courts[index] = court;

		public static Court GetById(string? id) => _courts.Count == 0 ? throw new InvalidOperationException("Ещё не было создано ни одной площадки") 
			: string.IsNullOrEmpty(id) ? throw new ArgumentException($"Введите идентификатор площадки")
			: int.TryParse(id, out int index) ? Contains(index) ? _courts[index] : throw new IndexOutOfRangeException("Отсутствует площадка с идентификатором " + id)
			: throw new InvalidCastException($"\"{id}\" не является идентификатором площадки");
	}
}