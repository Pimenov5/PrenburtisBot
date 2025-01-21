using System.Reflection;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal class BotCommandGroupFormBase : GroupForm
	{
		public override async Task Render(MessageResult message)
		{
			const string METHOD_NAME = "RenderAsync";
			MethodInfo? methodInfo = this.GetType().GetMethod(METHOD_NAME, [message.GetType()]);
			if (methodInfo is null)
				return;

			TextMessage? textMessage;
			try
			{
				object? result = methodInfo.Invoke(this, [message]);
				if (result is Task task)
					await task;

				textMessage = result switch
				{
					null => null,
					string => new((string)result),
					TextMessage => (TextMessage)result,
					Task<string> => new(await (Task<string>)result),
					Task<TextMessage> => await (Task<TextMessage>)result,
					_ => throw new InvalidCastException($"Неизвестный тип результата: {result.GetType().Name}")
				};
			}
			catch (Exception e)
			{
				textMessage = new TextMessage(e.Message).NavigateToStart();
			}

			if (textMessage is null)
				return;

			if (Environment.GetEnvironmentVariable("DELETE_APPEAL_TO_BOT_IN_GROUPS") is string value && bool.TryParse(value, out bool mustDelete) && mustDelete)
				await this.Device.DeleteMessage(message.MessageId);

			if (!string.IsNullOrEmpty(textMessage.Text))
			{
				await this.Client.TelegramClient.SendTextMessageAsync(this.Device.DeviceId, textMessage.Text, message.Message.Chat.IsForum ?? false ? message.Message.MessageThreadId : null,
				textMessage.ParseMode);
			}

			if (textMessage.NavigateTo.Form is not null)
				await this.Device.ActiveForm.NavigateTo(textMessage.NavigateTo.Form, textMessage.NavigateTo.Args);
		}
	}
}