using TelegramBotBase.Markdown;

﻿namespace PrenburtisBot.Types
{
	internal enum Gender { Male, Female };
	internal class Player(long userId, int rank, string firstName, double rating, bool isActual, Gender gender)
	{
		private string _firstName = firstName;

		public readonly long UserId = userId;
		public readonly int Rank = rank;
		public string FirstName { get { return _firstName; } set { _firstName = string.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(FirstName)) : value; } }
		public string? Username;
		public readonly double Rating = rating;
		public readonly bool IsActual = isActual;
		public readonly Gender Gender = gender;

		public string Link => $"tg://user?id={this.UserId}".Link(this.FirstName);
		public override string ToString() => this.Link;
	}
}