namespace PrenburtisBot.Types
{
	internal class User(long userId, string firstName, double rating, Gender gender, Skills skills, bool isArchived) 
		: Player(userId, firstName, rating, gender, skills)
	{
		public readonly bool IsArchived = isArchived;
	}
}