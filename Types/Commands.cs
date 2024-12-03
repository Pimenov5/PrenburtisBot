using PrenburtisBot.Attributes;
using PrenburtisBot.Forms;
using System.Reflection;
using System.Text;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal static class Commands
	{
		private static readonly Dictionary<string, KeyValuePair<Type, string?>> _commands = [];
		static Commands()
		{
			IEnumerable<Type> types = typeof(Start).Assembly.GetTypes().Where((Type type) => !type.Name.Contains('<') && type.Namespace is string typeNamespace && typeNamespace == typeof(Start).Namespace);
			foreach (Type type in types)
				_commands.Add(type.Name.ToString().ToLower(), new KeyValuePair<Type, string?>(type, type.GetCustomAttribute<BotCommandAttribute>() is BotCommandAttribute attribute ? attribute.Description : null));
		}

		public const char PARAMS_DELIMITER = '-';
		public static string ParamsToString(params string[] values) => values.Length == 0 ? string.Empty : new StringBuilder().AppendJoin(PARAMS_DELIMITER, values).ToString();

		public static bool Contains(string command) => _commands.ContainsKey(command);
		public static FormBase GetNewForm(string command) => _commands.TryGetValue(command, out KeyValuePair<Type, string?> type)
			? (FormBase)(type.Key.GetConstructor([])?.Invoke([]) ?? new AlertDialog($"Не удалось создать форму {command}", "OK")) : new AlertDialog($"Не удалось найти команду {command}", "OK");
		public static IEnumerable<KeyValuePair<string, string?>> GetCommands()
		{
			KeyValuePair<string, string?>[] commands = new KeyValuePair<string, string?>[_commands.Count];
			int i = 0;
			foreach (var command in _commands)
				commands[i++] = new(command.Key, command.Value.Value);

			return commands;
		}
	}
}