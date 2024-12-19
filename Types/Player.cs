namespace PrenburtisBot.Types
{
	internal class Player(long userId, int rank, string firstName, double rating)
	{
		public readonly long UserId = userId;
		public readonly int Rank = rank;
		public readonly string FirstName = firstName;
		public string? Username;
		public readonly double Rating = rating;

		public string Link => $"[{this.FirstName}](tg://user?id={this.UserId})";
		public override string ToString() => this.Link;
	}
}