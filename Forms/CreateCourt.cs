using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using TL;

namespace PrenburtisBot.Forms
{
	[BotCommand("Создать площадку с игроками из опроса", Telegram.Bot.Types.Enums.BotCommandScopeType.AllChatAdministrators)]
	internal class CreateCourt : AddPlayers
	{
		protected override Court GetCourt(int playersCount, long userId, out int courtId)
		{
			if (playersCount < 3)
				throw new ArgumentOutOfRangeException(nameof(playersCount), "Невозможно создать площадку, т.к. в опросе проголосовало меньше трёх игроков");
			else if (playersCount < 9)
				throw new ArgumentOutOfRangeException(nameof(playersCount), "Невозможно автоматически создать площадку, т.к. в опросе проголосовало меньше девяти игроков");

			List<KeyValuePair<uint, double>> infos = [ new(6, 0), new(7, 0), new(5, 0) ];
			for (int i = 0; i < infos.Count; i++)
				infos[i] = new(infos[i].Key, (double)playersCount / (double)infos[i].Key);

			infos.RemoveAll((KeyValuePair<uint, double> info) => info.Value < 1);
			int capacity = default;
			uint teamMaxPlayerCount = default;
			foreach (KeyValuePair<uint, double> item in infos)
				if (item.Value - Math.Truncate(item.Value) == 0)
				{
					capacity = (int)item.Value;
					teamMaxPlayerCount = item.Key;
					break;
				}

			if (capacity == default)
			{
				infos.Sort((KeyValuePair<uint, double> x, KeyValuePair<uint, double> y) => Math.Truncate(x.Value).CompareTo(Math.Truncate(y.Value)));
				double teamCount = Math.Truncate(infos[0].Value);
				infos.RemoveAll((KeyValuePair<uint, double> info) => Math.Truncate(info.Value) != teamCount);

				infos.Sort((KeyValuePair<uint, double> x, KeyValuePair<uint, double> y) => (y.Value - Math.Truncate(y.Value)).CompareTo(x.Value - Math.Truncate(x.Value)));
				capacity = infos[0].Value - Math.Truncate(infos[0].Value) == 0 ? (int)(infos[0].Value) : (int)infos[0].Value + 1;
				teamMaxPlayerCount = infos[0].Key;
			}

			List<Team> teams = new(capacity);
			string[] names = Team.Names;
			Random? random = names.Length >= teams.Capacity ? new() : null;
			for (int i = 0; i < teams.Capacity; i++)
				teams.Add(new(random is null ? null : names[random.Next(names.Length)]));

			RankedCourt court = new(userId, teams, teamMaxPlayerCount);
			courtId = Courts.Add(court);
			return court;
		}
	}
}