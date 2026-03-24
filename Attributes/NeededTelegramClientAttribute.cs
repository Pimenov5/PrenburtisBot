namespace PrenburtisBot.Attributes
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	internal class NeededTelegramClientAttribute(string propertyName = "TelegramClient") : Attribute
	{
		public readonly string PropertyName = propertyName;
	}
}