namespace PrenburtisBot.Types
{
	internal class User(long userId, string firstName, double rating, Gender gender, bool canVote, bool needVote) : Player(userId, firstName, rating, gender)
	{
		public readonly bool CanVote = canVote;
		public readonly bool NeedVote = needVote;
	}
}