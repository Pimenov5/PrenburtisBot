using PrenburtisBot.Forms;
using System.Reflection;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal class BotCommandGroupFormBase : GroupForm
	{
		private object[]? _initArgs;

		protected bool MustNavigateToStart = true;

		public BotCommandGroupFormBase() => this.Init += (object sender, TelegramBotBase.Args.InitEventArgs initArgs) =>
		{
			_initArgs = initArgs.Args;
			return Task.CompletedTask;
		};

		protected virtual async Task AfterMessagesSentAsync(IReadOnlyCollection<Telegram.Bot.Types.Message> messages, int? messageThreadId) { }

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

			IEnumerable<TextMessage>? textMessages;
			try
			{
				object? result = methodInfo.Invoke(this, parameters);
				if (result is Task task)
					await task;

				textMessages = result switch
				{
					null => null,
					string => [new((string)result)],
					TextMessage => [(TextMessage)result],
					Task<string> => [new(await (Task<string>)result)],
					Task<TextMessage> => [await (Task<TextMessage>)result],
					Task<IReadOnlyList<TextMessage>> => await (Task<IReadOnlyList<TextMessage>>)result,
					_ => throw new InvalidCastException($"Неизвестный тип результата: {result.GetType().Name}")
				};
			}
			catch (Exception e)
			{
				textMessages = [new TextMessage(e.Message).SetErrorKind().NavigateToStart()];
			}

			if (textMessages is null)
				return;

			if (Environment.GetEnvironmentVariable("DELETE_APPEAL_TO_BOT_IN_GROUPS") is string value && bool.TryParse(value, out bool mustDelete) && mustDelete)
				await message.DeleteMessage();

			int? messageThreadId = message.Message.Chat.IsForum ? message.Message.MessageThreadId : null;
			List<Telegram.Bot.Types.Message> newMessages = new(textMessages.Count());
			foreach (TextMessage textMessage in textMessages)
				if (!string.IsNullOrEmpty(textMessage.Text))
				{
					bool mustSendError = bool.TryParse(Environment.GetEnvironmentVariable("SEND_ERROR_MESSAGE_IN_GROUPS") ?? bool.TrueString, out bool boolValue) && boolValue;
					if (textMessage.Kind != TextMessageKind.Error || mustSendError)
					{
						Telegram.Bot.Types.Message newMessage = await this.API.SendMessage(this.Device.DeviceId, textMessage.Text, textMessage.ParseMode, textMessage.ReplyToMessageId,
							messageThreadId: messageThreadId);
						newMessages.Add(newMessage);
					}
					else
						Console.Error.WriteLine(textMessage.Text);
				}

			await AfterMessagesSentAsync(newMessages, messageThreadId);

			FormWithArgs? formWithArgs = null;
			foreach (TextMessage textMessage in textMessages) 
			{
				if (textMessage.NavigateTo.Form is not null)
					formWithArgs = formWithArgs is null ? textMessage.NavigateTo : throw new InvalidOperationException("Невозможно перейти на несколько форм одновременно");
			}

			formWithArgs ??= this.MustNavigateToStart ? new(new Start(), Start.SET_QUIET) : null;
			if (formWithArgs is not null)
				await this.Device.ActiveForm.NavigateTo(formWithArgs?.Form, formWithArgs?.Args);
		}
	}
}