using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal abstract class BotCommandFormBase : FormBase
	{
		private object[] _args = [];

		protected virtual async Task<string?> RenderAsync(long userId, params string[] args) { return null; }

		public BotCommandFormBase() => this.Init += async (object sender, TelegramBotBase.Args.InitEventArgs initArgs) =>
		{
			_args = initArgs.Args;
		};

		public override async Task Render(MessageResult message)
		{
			string[] args = message.BotCommandParameters.ToArray();
			if (this.Device.IsGroup) {
				if (!string.IsNullOrEmpty(message.BotCommand) && args.Length > 0 && args[^1].StartsWith('@') && args[^1] == "@" + (await this.Client.TelegramClient.GetMeAsync()).Username)
					Array.Resize(ref args, args.Length - 1);
				else
					return;
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

			string? text;
			try
			{
				text = await RenderAsync(this.Device.IsGroup && message.Message is Message messageFrom && messageFrom.From is User user ? user.Id : this.Device.DeviceId, args);
			}
			catch (Exception e)
			{
				text = e.Message;
			}

			if (!string.IsNullOrEmpty(text))
				await this.Device.Send(text);
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