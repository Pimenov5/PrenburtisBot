namespace PrenburtisBot.Types
{
	internal class User(long userId, string firstName, double rating, Gender gender, Skills skills, bool canVote, bool needVote, bool isArchived) 
		: Player(userId, firstName, rating, gender, skills)
	{
		public readonly bool CanVote = canVote;
		public readonly bool NeedVote = needVote;
		public readonly bool IsArchived = isArchived;
	}
}