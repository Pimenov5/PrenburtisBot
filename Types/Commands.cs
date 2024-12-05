using PrenburtisBot.Attributes;
using PrenburtisBot.Forms;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal static class Commands
	{
		private static readonly Dictionary<string, Type> _types = [];
		private static readonly Dictionary<BotCommandScope, List<BotCommand>> _commands = [];
		static Commands()
		{
			IEnumerable<Type> types = typeof(Start).Assembly.GetTypes().Where((Type type) => !type.Name.Contains('<') && type.Namespace is string typeNamespace && typeNamespace == typeof(Start).Namespace);
			foreach (Type type in types)
			{
				if (type.GetCustomAttribute<BotCommandAttribute>() is not BotCommandAttribute attribute)
					continue;

				BotCommandScope? botCommandScope = null;
				foreach (BotCommandScope key in _commands.Keys)
					if (key.Type == attribute.Scope)
					{
						botCommandScope = key;
						break;
					}
				
				botCommandScope ??= attribute.Scope switch { 
					BotCommandScopeType.Default => new BotCommandScopeDefault(), 
					BotCommandScopeType.AllPrivateChats => new BotCommandScopeAllPrivateChats(),
					BotCommandScopeType.AllGroupChats => new BotCommandScopeAllGroupChats(),
					BotCommandScopeType.AllChatAdministrators => new BotCommandScopeAllChatAdministrators(),
					BotCommandScopeType.Chat => new BotCommandScopeChat() { ChatId = ((BotCommandChatAttribute)attribute).ChatId },
					BotCommandScopeType.ChatAdministrators => new BotCommandScopeChatAdministrators() { ChatId = ((BotCommandChatAttribute)attribute).ChatId },
					BotCommandScopeType.ChatMember => new BotCommandScopeChatMember() { ChatId = ((BotCommandChatAttribute)attribute).ChatId, UserId = ((BotCommandChatMemberAttribute)attribute).UserId }
				};

				_commands.TryAdd(botCommandScope, []);
				BotCommand botCommand = new() { Command = type.Name.ToLower(), Description = attribute.Description };
				_commands[botCommandScope].Add(botCommand);

				_types.Add(botCommand.Command, type);
			}
		}

		public const char PARAMS_DELIMITER = '-';
		public static string ParamsToString(params string[] values) => values.Length == 0 ? string.Empty : new StringBuilder().AppendJoin(PARAMS_DELIMITER, values).ToString();

		public static bool Contains(string command) => _types.ContainsKey(command);
		public static FormBase GetNewForm(string command) => _types.TryGetValue(command, out Type? type)
			? (FormBase)(type.GetConstructor([])?.Invoke([]) ?? new AlertDialog($"Не удалось создать форму {command}", "OK")) : new AlertDialog($"Не удалось найти команду {command}", "OK");
		public static IReadOnlyDictionary<BotCommandScope, List<BotCommand>> GetCommands() => new ReadOnlyDictionary<BotCommandScope, List<BotCommand>>(_commands);
	}
}