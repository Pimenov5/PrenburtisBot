using System.Text;
using TelegramBotBase.Markdown;

﻿namespace PrenburtisBot.Types
{
	internal enum Gender { Male, Female };

	internal enum Skill { Passing = 1, Setting = 2, Attacking = 3};

	internal readonly struct Skills(double passing, double setting, double attacking)
	{
		private static double SetCheck(double value) => 1 <= value && value <= 5 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Значение навыка должно быть от 1 до 5");

		public readonly double Passing = SetCheck(passing);
		public readonly double Setting = SetCheck(setting);
		public readonly double Attacking = SetCheck(attacking);

		public double this[Skill skill] => skill switch { Skill.Passing => this.Passing, Skill.Setting => this.Setting, Skill.Attacking => this.Attacking, 
			_ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Неизвестный тип навыка") };

		public string ToString(IEnumerable<Skill> order, double averageValue = 1, bool isNumericFormat = true, string separator = "'", string? prefix = null)
		{
			if (Environment.GetEnvironmentVariable("SHOW_PLAYER_SKILLS") is not string showSkills || !bool.TryParse(showSkills, out bool boolValue) || !boolValue)
				return string.Empty;

			List<char> values = [];
			foreach (Skill skill in order)
				if (this[skill] > averageValue)
				{
					values.Add((isNumericFormat ? ((int)skill).ToString() : skill.ToString()).First());
				}

			return $"{prefix}{new StringBuilder().AppendJoin(separator, values)}";
		}
	}

	internal class Player(long userId, string firstName, double rating, Gender gender, Skills skills)
	{
		private string _firstName = firstName;

		public readonly long UserId = userId;
		public int Rank => (int)Math.Truncate(this.Rating);
		public string FirstName { get { return _firstName; } set { _firstName = string.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(FirstName)) : value; } }
		public string? Username;
		public readonly double Rating = rating;
		public readonly Gender Gender = gender;
		public readonly Skills Skills = skills;

		public string Link => $"tg://user?id={this.UserId}".Link(this.FirstName) + this.Skills.ToString([Skill.Attacking, Skill.Setting, Skill.Passing], prefix: " ");
		public override string ToString() => this.Link;
	}
}