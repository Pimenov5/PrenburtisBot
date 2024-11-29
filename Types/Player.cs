namespace PrenburtisBot.Types
{
	internal class Player(long userId, int rank, string firstName)
	{
		public readonly long UserId = userId;
		public readonly int Rank = rank;
		public readonly string FirstName = firstName;

		public string Link => $"[{this.FirstName}](tg://user?id={this.UserId})";
	}
}