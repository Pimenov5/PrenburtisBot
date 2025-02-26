using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.BeforeBotStart
{
	[BeforeBotStartExecutable(nameof(ReadTeamsNames.FromFile))]
	internal static class ReadTeamsNames
	{
		public static string FromFile()
		{
			string path = BeforeBotStartExecutableAttribute.GetPath("TEAMS_NAMES");
			using StreamReader streamReader = new(path);
			return $"Добавлены имена команд ({Team.ReadNames(streamReader)}) из файла {path}";
		}
	}
}