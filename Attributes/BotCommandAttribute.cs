namespace PrenburtisBot.Attributes
{
	internal class BotCommandAttribute(string description) : Attribute
	{
		public readonly string Description = description;
	}
}