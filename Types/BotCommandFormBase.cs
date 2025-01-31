using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal abstract class BotCommandFormBase : FormBase
	{
		private object[] _args = [];

		public BotCommandFormBase() => this.Init += async (object sender, TelegramBotBase.Args.InitEventArgs initArgs) =>
		{
			_args = initArgs.Args;
		};

		public override async Task Render(MessageResult message)
		{
			List<string> botCommandParameters = message.BotCommandParameters.Count > 0 ? message.BotCommandParameters : (!string.IsNullOrEmpty(message.BotCommand)
				&& message.Command.Contains(message.BotCommand) ? message.Command.Replace(message.BotCommand, string.Empty) : message.Command).Split(' ').ToList();
			botCommandParameters.RemoveAll((string value) => string.IsNullOrEmpty(value));
			string[] args = botCommandParameters.ToArray();

			if (this.Device.IsGroup) {
				string botUsername = '@' + (await this.Client.TelegramClient.GetMeAsync()).Username;
				if (!string.IsNullOrEmpty(message.BotCommand) && args.Length > 0 && args[^1] == botUsername)
					Array.Resize(ref args, args.Length - 1);
				else
				{
					if (!message.Command.EndsWith(botUsername) && !string.IsNullOrEmpty(message.BotCommand))
						Console.WriteLine($"Предотвращена попытка вызова {this.GetType().Name} без указания {botUsername} в групповом чате {this.Device.DeviceId}");
					return;
				}
			}

			if (_args.Length == 0)
			{
				args = args.Length == 1 && args[0].Contains(Commands.PARAMS_DELIMITER) ? args[0].Split(Commands.PARAMS_DELIMITER)[1..] : args.Length > 0 ? args
					: string.IsNullOrEmpty(message.BotCommand) && !string.IsNullOrEmpty(message.Command) ? [message.Command] : [];
			}
			else
			{
				Array.Resize(ref args, _args.Length);
				int index = 0;
				for (int i = index; i < args.Length; i++)
					if (_args[i].ToString() is string argument)
						args[index++] = argument;

				Array.Resize(ref args, index);
				_args = [];
			}

			List<Type> types = new(1 + args.Length);
			List<Object> parameters = new(types.Capacity);
			for (int i = 0; i < args.Length; i++) {
				types.Add(args[i].GetType());
				parameters.Add(args[i]);
			}

			const string METHOD_NAME = "Render";
			const string ASYNC_METHOD_NAME = METHOD_NAME + "Async";
			System.Reflection.MethodInfo? methodInfo = this.GetType().GetMethod(METHOD_NAME, types.ToArray()) ?? this.GetType().GetMethod(ASYNC_METHOD_NAME, types.ToArray());
			if (methodInfo is null)
			{
				methodInfo = this.GetType().GetMethod(METHOD_NAME, [typeof(string[])]) ?? this.GetType().GetMethod(ASYNC_METHOD_NAME, [typeof(string[])]);
				if (methodInfo is not null)
				{
					parameters.Clear();
					parameters.Add(args);
				}
			}

			if (methodInfo is null)
			{
				long userId = this.Device.IsGroup && message.Message is Message messageFrom && messageFrom.From is User user ? user.Id : this.Device.DeviceId;
				types.Insert(0, userId.GetType());
				parameters.Insert(0, userId);
				methodInfo = this.GetType().GetMethod(METHOD_NAME, types.ToArray()) ?? this.GetType().GetMethod(ASYNC_METHOD_NAME, types.ToArray());

				if (methodInfo is null)
				{
					methodInfo = this.GetType().GetMethod(METHOD_NAME, [typeof(long), typeof(string[])]) ?? this.GetType().GetMethod(ASYNC_METHOD_NAME, [typeof(long), typeof(string[])]);
					if (methodInfo is not null)
					{
						parameters.RemoveRange(1, parameters.Count - 1);
						parameters.Add(args);
					}
				}
			}

			TextMessage? textMessage = null;
			try
			{
				if (methodInfo is null)
					throw new NullReferenceException("Невалидное количество параметров команды");

				if (methodInfo.Invoke(this, parameters.ToArray()) is object result) {
					result = result switch
					{
						Task<string> => await (Task<string>)result,
						Task<TextMessage> => await (Task<TextMessage>)result,
						_ => result
					};

					textMessage = result switch
					{
						null => null,
						string => new((string)result),
						TextMessage => (TextMessage)result,
						_ => throw new InvalidOperationException($"Неизвестный тип результата: {result.GetType().Name}")
					};
				}
		
			}
			catch (Exception e)
			{
				textMessage = new((e.InnerException ?? e).Message);
			}

			if (textMessage is not null)
			{
				if (!string.IsNullOrEmpty(textMessage.Text))
				{
					InlineKeyboardMarkup? inlineKeyboardMarkup = this.Device.IsGroup ? null : textMessage.Buttons;
					await this.Device.Client.TelegramClient.SendTextMessageAsync(this.Device.DeviceId, textMessage.Text,
						message.Message.Chat.IsForum ?? false ? message.Message.MessageThreadId : null, textMessage.ParseMode, replyMarkup: inlineKeyboardMarkup);
				}

				if (textMessage.NavigateTo.Form is not null)
					await this.Device.ActiveForm.NavigateTo(textMessage.NavigateTo.Form, textMessage.NavigateTo.Args);
			}
		}

		public override async Task Action(MessageResult message)
		{
			message.Handled = true;
			CallbackData callback = message.GetData<CallbackData>();
			string command = callback.Method.ToLower();
			if (Commands.Contains(command))
			{
				await message.ConfirmAction();
				object[] args = callback.Value.Contains(Commands.PARAMS_DELIMITER) ? callback.Value.Split(Commands.PARAMS_DELIMITER) : [callback.Value];
				for (int i = 0; i < args.Length; i++)
					if (args[i] is string argument && Environment.GetEnvironmentVariable("MESSAGE_ID_ALIAS") is string messageIdAlias && argument == messageIdAlias)
						args[i] = message.MessageId.ToString();


				await this.Device.ActiveForm.NavigateTo(Commands.GetNewForm(command), args);
			}
			else
				await message.ConfirmAction($"\"{command}\" не является командой");
		}
	}
}