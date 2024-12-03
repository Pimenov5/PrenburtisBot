using TelegramBotBase.Builder;
using TelegramBotBase.Commands;
using PrenburtisBot.Forms;
using TelegramBotBase.Args;
using TelegramBotBase.Form;
using PrenburtisBot.Types;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using TelegramBotBase;
using System.Reflection;
using PrenburtisBot.Attributes;

namespace PrenburtisBot
{
    internal class Program
	{
		private static string? GetFilePath(string variable)
		{
			if (Environment.GetEnvironmentVariable(variable) is string path)
			{
				if (File.Exists(path))
					return path;
				else
					Console.WriteLine($"Не существует файл {path}");
			}
			else
				Console.WriteLine($"Отсутствует переменная окружения {variable}");

			return null;
		}

		private static async Task Main(string[] args)
		{
			if (GetFilePath("TEAMS_NAMES") is string path)
			{
				using StreamReader streamReader = new(path);
				Console.WriteLine($"Добавлены имена команд ({Team.ReadNames(streamReader)}) из файла {path}");
			}

			if (GetFilePath("PRENBURTIS_DATA_BASE") is string dataSource) {
				SqliteConnectionStringBuilder connectionStringBuilder = new() { Mode = SqliteOpenMode.ReadOnly, DataSource = dataSource };
				using SqliteConnection connection = new(connectionStringBuilder.ConnectionString);
				try
				{
					connection.Open();
					Console.WriteLine($"Установлено соединение с {connection.DataSource}");

					SqliteCommand command = new("SELECT * FROM users", connection);
					SqliteDataReader reader = command.ExecuteReader();
					Console.WriteLine($"Добавлено ранговых игроков: {Users.Read(reader)}");

				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}

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
						if (command.Value is string description)
							if (typeof(Start).Assembly.GetType(typeof(Start).Namespace + '.' + command.Key, false, true) is Type type)
							{
								if (type.GetCustomAttribute(typeof(GroupAdminCommandAttribute)) is null)
									action.AddPrivateChatCommand(command.Key, description);
								else
									action.AddGroupAdminCommand(command.Key, description);
							}
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
				await args.Device.ActiveForm.NavigateTo(newForm);
			};

			await bot.UploadBotCommands();

			await bot.Start();
			Console.WriteLine($"Бот @{(await bot.Client.TelegramClient.GetMeAsync()).Username} запущен и работает");
			while (true) { }
		}
	}
}
