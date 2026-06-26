namespace PrenburtisBot.Types
{
	internal readonly struct Season(int id, DateOnly firstDate, DateOnly lastDate)
	{
		public readonly int Id = id;
		public readonly DateOnly FirstDate = firstDate;
		public readonly DateOnly LastDate = lastDate;

		public List<DateOnly> ParseDates(IEnumerable<string> items)
		{
			const string ALL_ALIAS = "all";
			List<string> strings = [..items];

			if (strings.Count > 0 && strings[0].Equals(ALL_ALIAS, StringComparison.OrdinalIgnoreCase) 
				&& Environment.GetEnvironmentVariable("OPEN_SEASON_POLL_OPTIONS") is string strOptions && strOptions.Split(Commands.PARAMS_DELIMITER) is string[] options
				&& options.Max((string option) => decimal.TryParse(option, out decimal value) ? value : null) is decimal maxOption)
			{
				string strMaxOption = maxOption.ToString();
				List<string> strDays = new(strMaxOption.Length);
				foreach (char number in strMaxOption)
				{
					DayOfWeek dayOfWeek = (DayOfWeek)(int.Parse(number.ToString()));
					strDays.Add(Enum.GetName(dayOfWeek) ?? throw new NullReferenceException());
				}

				if (strDays.Count > 0)
				{
					strings.RemoveAt(0);
					strings.InsertRange(0, strDays);
				}
				else
					throw new("Не удалось определить все дни недели с тренировками в абонементе");
			}

			List<DayOfWeek> days = [];
			List<int> includeDates = [], excludeDates = [];
			foreach (string dayStr in strings)
			{
				if (dayStr.Length > 1 && dayStr[0] is char sign && (sign == '-' || sign == '+') && int.TryParse(dayStr[1..], out int date))
				{
					if (includeDates.Contains(date) || excludeDates.Contains(date))
						throw new ArgumentException($"Нельзя указывать дату более одного раза: {date}");

					List<int> list = sign == '+' ? includeDates : excludeDates;
					list.Add(date);
				}
				else if (Enum.TryParse(dayStr, out DayOfWeek day))
					days.Add(day);
				else
					throw new ArgumentException("Не удалось интерпритировать строку: " + dayStr, nameof(items));
			}

			List<DateOnly> result = [];
			for (DateOnly date = this.FirstDate; date <= this.LastDate; date = date.AddDays(1))
				if ((days.Contains(date.DayOfWeek) || includeDates.Contains(date.Day)) && !excludeDates.Contains(date.Day))
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