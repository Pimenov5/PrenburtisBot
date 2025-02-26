using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.BeforeBotStart
{
	[BeforeBotStartExecutable(nameof(ReadSession.FromFile))]
	internal static class ReadSession
	{
		public static string FromFile()
		{
			Session.Path = BeforeBotStartExecutableAttribute.GetPath("SESSION_PATH");
			return Session.Read() ? $"Добавлены данные сессии из файла {Session.Path}" : $"Не удалось добавить данные сессии из файла {Session.Path}";
		}
	}
}