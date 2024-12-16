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

		public static Court GetById(string? id) => GetById(ref id, null);
		public static Court GetById(ref string? id, long? userId)
		{
			if (_courts.Count == 0)
				throw new InvalidOperationException("Ещё не было создано ни одной площадки");
			else if (string.IsNullOrEmpty(id)) {
				const string NEED_COURT_ID = "Введите идентификатор площадки";
				if (userId is null)
					throw new ArgumentException(NEED_COURT_ID);
				else
				{
					Court? result = null;
					Court? GetByUserId() {
						foreach (Court court in _courts)
							if (court.UserId == userId || court.ContainsPlayer(userId ?? throw new NullReferenceException()))
							{
								if (result is null)
									return court;
								else if (result != court)
									return null;
							}

						return result;
					}

					result = GetByUserId();
                    if (result is not null)
						result = GetByUserId();

					id = result is null ? id : _courts.IndexOf(result).ToString();
					return result ?? throw new ArgumentException(NEED_COURT_ID);
				}
			}
			else if (int.TryParse(id, out int index)) {
				if (Contains(index))
					return _courts[index];
				else
					throw new IndexOutOfRangeException("Отсутствует площадка с идентификатором " + id);
			}
			else
				throw new InvalidCastException($"\"{id}\" не является идентификатором площадки");
		}
	}
}