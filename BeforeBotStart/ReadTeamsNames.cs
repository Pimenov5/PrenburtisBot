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
			int count = Team.ReadNames(streamReader);
			return $"Добавлены имена команд ({count + (Team.Names.Count == count ? string.Empty : $" => {Team.Names.Count}")}) из файла {path}";
		}
	}
}