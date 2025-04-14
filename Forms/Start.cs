using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Запустить бота или отменить выполнение текущей команды")]
	internal class Start : BotCommandFormBase
	{
		public string? Render() => Render(null);
		public string? Render(string? mode)
		{
			return this.Device.IsGroup || this.Device.IsChannel || mode == SET_QUIET ? null : "Введите команду или выберите из меню";  
		}

		public async Task<string?> RenderAsync(string strChatId, string strIsConfirmed, string fileId)
		{
			static ArgumentException GetException(string value, string format = "Не удалось выделить {0}") => new(string.Format(format, value));

			ChatId chatId = long.TryParse(strChatId, out long longValue) ? longValue : throw GetException("идентификатор пользователя");

			string text = string.Empty;
			try
			{
				bool isConfirmed = bool.TryParse(strIsConfirmed, out bool boolValue) ? boolValue : throw GetException("разрешение на обновление");

				if (isConfirmed)
				{
					fileId = string.IsNullOrEmpty(fileId) ? throw GetException($"идентификатор файла") : fileId;
					using MemoryStream memoryStream = new();
					Telegram.Bot.Types.File file = await this.Client.TelegramClient.GetInfoAndDownloadFileAsync(fileId, memoryStream);
					string? path = Environment.GetEnvironmentVariable("TEAMS_NAMES") ?? throw new NullReferenceException();
					System.IO.File.WriteAllBytes(path, memoryStream.GetBuffer());

					text = "Файл успешно обновлён";
				}
				else
					text = "Обновление файла было отклонено";
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				text = e.Message;
			}

			if (chatId.Identifier != this.Device.DeviceId)
				await this.Client.TelegramClient.SendTextMessageAsync(chatId, text);
			return text;
		}

		public override async Task PreLoad(MessageResult message)
		{
			if (message.IsFirstHandler && message.BotCommandParameters.Count == 1 && message.BotCommandParameters[0].Split(Commands.PARAMS_DELIMITER) is string[] link
				&& link.Length >= 1 && Commands.Contains(link[0]))
			{
				await this.NavigateTo(Commands.GetNewForm(link[0]), link.Length == 0 ? [] : link[1..]);
			}
		}

		public const string SET_QUIET = "sqe";

		public static async Task<string> GetDeepLinkAsync(ITelegramBotClient botClient, Type type, params string[] args) => $"t.me/{(await botClient.GetMeAsync()).Username}"
			+ $"?{nameof(Start).ToLower()}={type.Name.ToLower()}" + (args.Length == 0 ? string.Empty : Commands.PARAMS_DELIMITER + Commands.ParamsToString(args));
	}
}