namespace PrenburtisBot.Types
{
	internal readonly struct Season(int id, DateOnly firstDate, DateOnly lastDate)
	{
		public readonly int Id = id;
		public readonly DateOnly FirstDate = firstDate;
		public readonly DateOnly LastDate = lastDate;

		public List<DateOnly> ParseDates(IEnumerable<string> strings)
		{
			List<DayOfWeek> days = [];
			foreach (string dayStr in strings)
			{
				if (!Enum.TryParse(dayStr, out DayOfWeek day))
					throw new ArgumentException($"\"{dayStr}\" не является валидным днём недели", nameof(strings));

				days.Add(day);
			}

			List<DateOnly> result = [];
			for (DateOnly date = this.FirstDate; date <= this.LastDate; date = date.AddDays(1))
				if (days.Contains(date.DayOfWeek))
					result.Add(date);

			return result;
		}

		public static string NumbersToDays(string numbers, string separator)
		{
			List<string> days = new(numbers.Length);
			foreach (char number in numbers.ToCharArray())
				days.Add(Enum.GetName((DayOfWeek)int.Parse([number])) ?? throw new NullReferenceException($"Не удалось преобразовать {number} в день недели"));

			return string.Join(separator, days);
		}
	}
}