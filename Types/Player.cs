namespace PrenburtisBot.Types
{
	internal class Player(long userId, int rank, string firstName, double rating)
	{
		private string _firstName = firstName;

		public readonly long UserId = userId;
		public readonly int Rank = rank;
		public string FirstName { get { return _firstName; } set { _firstName = string.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(FirstName)) : value; } }
		public string? Username;
		public readonly double Rating = rating;

		public string Link => $"[{this.FirstName}](tg://user?id={this.UserId})";
		public override string ToString() => this.Link;
	}
}