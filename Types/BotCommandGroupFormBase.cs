using System.Reflection;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal class BotCommandGroupFormBase : GroupForm
	{
		private object[]? _initArgs;

		public BotCommandGroupFormBase() => this.Init += (object sender, TelegramBotBase.Args.InitEventArgs initArgs) =>
		{
			_initArgs = initArgs.Args;
			return Task.CompletedTask;
		};

		public override async Task Render(MessageResult message)
		{
			Type[] types;
			object?[] parameters;
			if (_initArgs is not null && _initArgs.Length > 0)
			{
				types = new Type[_initArgs.Length];
				parameters = new object?[types.Length];
				for (int i = 0; _initArgs is not null && i < _initArgs.Length; i++)
				{
					types[i] = _initArgs[i].GetType();
					parameters[i] = _initArgs[i];
				}
			}
			else
			{
				types = [message.GetType()];
				parameters = [message];
			}

			const string METHOD_NAME = "RenderAsync";
			MethodInfo? methodInfo = this.GetType().GetMethod(METHOD_NAME, types);
			if (methodInfo is null)
				return;

			TextMessage? textMessage;
			try
			{
				object? result = methodInfo.Invoke(this, parameters);
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
				textMessage = new TextMessage(e.Message).SetErrorKind().NavigateToStart();
			}

			if (textMessage is null)
				return;

			if (Environment.GetEnvironmentVariable("DELETE_APPEAL_TO_BOT_IN_GROUPS") is string value && bool.TryParse(value, out bool mustDelete) && mustDelete)
				await message.DeleteMessage();

			if (!string.IsNullOrEmpty(textMessage.Text))
			{
				bool mustSendError = bool.TryParse(Environment.GetEnvironmentVariable("SEND_ERROR_MESSAGE_IN_GROUPS") ?? bool.TrueString, out bool boolValue) && boolValue;
				if (textMessage.Kind != TextMessageKind.Error || mustSendError)
				{
					await this.API.SendMessage(this.Device.DeviceId, textMessage.Text, textMessage.ParseMode, textMessage.ReplyToMessageId,
						messageThreadId: message.Message.Chat.IsForum ? message.Message.MessageThreadId : null);
				}
				else
					Console.Error.WriteLine(textMessage.Text);
			}

			if (textMessage.NavigateTo.Form is not null)
				await this.Device.ActiveForm.NavigateTo(textMessage.NavigateTo.Form, textMessage.NavigateTo.Args);
		}
	}
}