﻿using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Запустить бота или отменить выполнение текущей команды")]
	internal class Start : FormBase
	{
		public override async Task PreLoad(MessageResult message)
		{
			if (message.BotCommandParameters.Count == 1 && message.BotCommandParameters[0].Split(Commands.PARAMS_DELIMITER) is string[] link
				&& link.Length >= 1 && Commands.Contains(link[0]))
			{
				await this.NavigateTo(Commands.GetNewForm(link[0]), link.Length == 0 ? [] : link[1..]);
			}
		}

		public override async Task Render(MessageResult message) {
			if (this.Device.IsGroup)
				return;

			await this.Device.Api(async (ITelegramBotClient botClient) => await botClient.SendTextMessageAsync(this.Device.DeviceId, "Введите команду или выберите из меню",
				message.Message.MessageThreadId));
		}

		public static async Task<string> GetDeepLinkAsync(ITelegramBotClient botClient, Type type, params string[] args) => $"t.me/{(await botClient.GetMeAsync()).Username}"
			+ $"?{nameof(Start).ToLower()}={type.Name.ToLower()}" + (args.Length == 0 ? string.Empty : Commands.PARAMS_DELIMITER + Commands.ParamsToString(args));
	}
}