﻿using PrenburtisBot.Attributes;
using PrenburtisBot.BeforeBotStart;
using PrenburtisBot.Types;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotBase.Base;
using TelegramBotBase.Form;
using TelegramBotBase.Markdown;

namespace PrenburtisBot.Forms
{
	[BotCommand("Запустить бота или отменить выполнение текущей команды")]
	internal class Start : BotCommandFormBase
	{
		public override async Task Render(MessageResult message)
		{
			if (this.InitArgs.Length == 1 && this.InitArgs[0].Equals(SET_QUIET))
				return;
			else
				await base.Render(message);
		}

		public async Task<string?> RenderAsync() => await RenderAsync(null);
		public async Task<string?> RenderAsync(string? mode)
		{
			if (this.Device.LastMessage is Message message && message.From is Telegram.Bot.Types.User user && message.Document is Document document && document.FileName is string documentFileName
				&& Environment.GetEnvironmentVariable("TEAMS_NAMES") is string fileName && documentFileName.Equals(Path.GetFileName(fileName)))
			{
				if (!long.TryParse(Environment.GetEnvironmentVariable("BOT_OWNER_CHAT_ID"), out long botOwnerChatId))
					throw new Exception("Отсутствует идентификатор владельца бота");

				IReplyMarkup replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Да",
					new CallbackData(nameof(Start), Commands.ParamsToString(user.Id.ToString(), bool.TrueString))),
					InlineKeyboardButton.WithCallbackData("Нет", new CallbackData(nameof(Start), Commands.ParamsToString(user.Id.ToString(), bool.FalseString))));
				await this.API.SendDocument(botOwnerChatId, InputFile.FromFileId(document.FileId),
					caption: $"{user.Id.ToString().Link(user.FirstName)} пытается обновить этот файл. Разрешить?",
					parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, replyMarkup: replyMarkup);

				return "Отправлен запрос на обновление файла";
			}

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
					string? path = Environment.GetEnvironmentVariable("TEAMS_NAMES") ?? throw new NullReferenceException();
					using (FileStream fileStream = System.IO.File.OpenWrite(path))
					{
						fileStream.SetLength(0);
						await this.API.GetInfoAndDownloadFile(fileId, fileStream);
					}

					text = "Файл успешно обновлён";
					text += Environment.NewLine + ReadTeamsNames.FromFile();
				}
				else
					text = "Обновление файла было отклонено";
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				text += Environment.NewLine + e.Message;
			}

			if (chatId.Identifier != this.Device.DeviceId)
				await this.API.SendMessage(chatId, text);
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

		public static async Task<string> GetDeepLinkAsync(ITelegramBotClient botClient, Type type, params string[] args) => $"t.me/{(await botClient.GetMe()).Username}"
			+ $"?{nameof(Start).ToLower()}={type.Name.ToLower()}" + (args.Length == 0 ? string.Empty : Commands.PARAMS_DELIMITER + Commands.ParamsToString(args));
	}
}