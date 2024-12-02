using PrenburtisBot.Forms;
using Telegram.Bot;
using TelegramBotBase.Base;
using TelegramBotBase.Form;
using System.Text;

namespace PrenburtisBot.Types
{
	internal abstract class LinkedForm : FormBase
	{
		private const char PARAMS_DELIMITER = '-';
		private object[] _args = [];

		protected static string ParamsToString(params string[] values) => values.Length == 0 ? string.Empty : new StringBuilder().AppendJoin(PARAMS_DELIMITER, values).ToString();

		protected async Task<string> GetLinkAsync(Type type, params string[] args) => $"t.me/{(await this.Client.TelegramClient.GetMeAsync()).Username}?{nameof(Start).ToLower()}="
			+ $"{type.Name.ToLower()}" + (args.Length == 0 ? string.Empty : PARAMS_DELIMITER + ParamsToString(args));

		protected virtual async Task<string?> RenderAsync(params string[] args) { throw new NotImplementedException(); }

		public LinkedForm() => this.Init += async (object sender, TelegramBotBase.Args.InitEventArgs initArgs) =>
		{
			_args = initArgs.Args;
		};

		public override async Task PreLoad(MessageResult message)
		{
			if (message.MessageType != Telegram.Bot.Types.Enums.MessageType.Text)
				message.Handled = true;
			else if (message.BotCommand == "/" + nameof(Start).ToLower() && message.BotCommandParameters.Count == 1
				&& message.BotCommandParameters[0].Split(PARAMS_DELIMITER) is string[] link && link.Length >= 1 && Commands.Contains(link[0])
				&& this.Device.ActiveForm.GetType().Name.ToLower() != link[0])
			{
				await this.NavigateTo(Commands.GetNewForm(link[0]));
			}
		}

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
				args = args.Length == 1 && args[0].Contains(PARAMS_DELIMITER) ? args[0].Split(PARAMS_DELIMITER)[1..] : args.Length > 0 ? args
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
				text = await RenderAsync(args);
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
				object[] args = callback.Value.Contains(PARAMS_DELIMITER) ? callback.Value.Split(PARAMS_DELIMITER) : [callback.Value];
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