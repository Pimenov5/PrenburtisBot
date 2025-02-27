using TelegramBotBase.Builder;
using PrenburtisBot.Forms;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
using PrenburtisBot.Types;
using Telegram.Bot;
using TelegramBotBase;
using PrenburtisBot.Attributes;
using System.Reflection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PrenburtisBot
{
    internal class Program
	{
		private static async Task BeforeBotStartExecuteAsync(params string[] args)
		{
			if (args.Length == 0)
				return;

			List<Type> types = typeof(Program).Assembly.GetTypes().Where((Type type) => type.GetInterface(nameof(IBeforeBotStartAsyncExecutable)) is not null
				|| type.GetCustomAttribute<BeforeBotStartExecutableAttribute>() is not null).ToList();

			foreach (string commandName in args)
			{
				if (types.Find((Type type) => type.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase)) is not Type type)
				{
					Console.Error.WriteLine($"Не удалось найти \"{commandName}\"");
					continue;
				}

				try
				{
					string result = string.Empty;
					if (type.GetConstructor([])?.Invoke([]) is IBeforeBotStartAsyncExecutable command)
					{
						result = await command.ExecuteAsync() ?? string.Empty;
						if (command is IDisposable disposableCommand)
							disposableCommand.Dispose();
					}
					else if (type.GetCustomAttribute<BeforeBotStartExecutableAttribute>() is BeforeBotStartExecutableAttribute attribute)
					{
						if (type.GetMethod(attribute.MethodName) is not MethodInfo methodInfo)
						{
							Console.Error.WriteLine($"У класса \"{commandName}\" отсутствует метод \"{attribute.MethodName}\"");
							continue;
						}

						if (methodInfo.Invoke(type, []) is string str)
							result = str;
					}
					else
						Console.Error.WriteLine($"Не удалось вызвать \"{commandName}\"");

					if (!string.IsNullOrEmpty(result))
						Console.WriteLine($"Результат \"{commandName}\": {result}");
				}
				catch (Exception e)
				{
					Console.Error.WriteLine($"Не удалось выполнить \"{commandName}\" из-за ошибки: {(e.InnerException ?? e).Message}");
				}
			}
		}

		private static async Task Main(string[] args)
		{
			const string API_KEY = "API_KEY";
			string? apiKey = Environment.GetEnvironmentVariable(API_KEY);
			if (string.IsNullOrEmpty(apiKey))
			{
				Console.WriteLine("Невозможно запустить бота, т.к. в переменных окружения отсутствует " + API_KEY);
				Console.ReadLine();
				return;
			}

			BotBase bot = BotBaseBuilder.Create()
				.WithAPIKey(apiKey)
				.DefaultMessageLoop()
				.WithStartForm<Start>()
				.NoProxy()
				.CustomCommands(action =>
				{
					foreach (var command in Commands.GetCommands())
						action.Add(command.Key, command.Value);
				})
				.NoSerialization()
				.UseRussian()
				.UseSingleThread()
				.Build();

			bot.Exception += (object? sender, SystemExceptionEventArgs args) =>
			{
				Console.WriteLine($"На форме {args.Device.ActiveForm.GetType().Name} при обработке \"{args.Command}\" на устройстве {args.DeviceId} возникла ошибка:"
					+ Environment.NewLine + args.Error.ToString() + Environment.NewLine);
			};

			bot.BotCommand += async (object sender, BotCommandEventArgs args) =>
			{
				FormBase newForm = Commands.GetNewForm(args.Command.StartsWith('/') ? args.Command[1..] : args.Command);
				Type type = newForm.GetType();
				if (type.GetCustomAttribute<BotCommandAttribute>() is BotCommandAttribute commandAttribute)
					switch (commandAttribute.Scope)
					{
						case BotCommandScopeType.Chat when commandAttribute is BotCommandChatAttribute attribute 
							&& Commands.GetChatId(attribute, type) is ChatId chatId && chatId != args.DeviceId:

						case BotCommandScopeType.AllChatAdministrators when args.Device.IsGroup && args.OriginalMessage.From?.Id is long userId
							&& ! new List<ChatMember>(await args.Device.Client.TelegramClient.GetChatAdministratorsAsync(args.DeviceId)).Any((ChatMember member) => member.User.Id == userId):

							Console.WriteLine($"Предотвращён вызов формы {type.Name} пользователем {args.OriginalMessage.From?.FirstName} в чате \"{args.Device.GetChatTitle()}\"");
							return;

					};

				await args.Device.ActiveForm.NavigateTo(newForm);
			};

			TextMessage.GetStartForm = () => new Start();
			Login.LoginEvent += (Type? type, WTelegram.Client client) => {
				RepliedToPollGroupFormBase.TelegramClient = client;
			};
			await bot.UploadBotCommands();

			await Program.BeforeBotStartExecuteAsync(args);

			await bot.Start();
			Console.WriteLine($"Бот @{(await bot.Client.TelegramClient.GetMeAsync()).Username} запущен и работает");
			while (true) { }
		}
	}
}
