using PrenburtisBot.Attributes;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Авторизация пользователя", null)]
	internal class Login : FormBase
	{
		private static WTelegram.Client? _client = null;
		private async Task<string?> RenderAsync(MessageResult message)
		{
			if (_client is not null)
				return "Вы уже были авторизованы";

			const string LOGIN_API_ID = "LOGIN_API_ID";
			if (!int.TryParse(Environment.GetEnvironmentVariable(LOGIN_API_ID), out int apiId))
				return $"Значение переменной окружения {LOGIN_API_ID} не является валидным App api_id";

			const string LOGIN_API_HASH = "LOGIN_API_HASH";
			if (Environment.GetEnvironmentVariable(LOGIN_API_HASH) is not string apiHash)
				return $"Значение переменной окружения {LOGIN_API_HASH} не является валидным App api_hash";

			const string LOGIN_PHONE_NUMBER = "LOGIN_PHONE_NUMBER";
			if (Environment.GetEnvironmentVariable(LOGIN_PHONE_NUMBER) is not string phoneNumber)
				return $"Значение переменной окружения {LOGIN_PHONE_NUMBER} не является валидным номером телефона";

			string loginInfo = phoneNumber;
			PromptDialog promptDialog = new();
			promptDialog.Completed += async (sender, args) =>
			{
				loginInfo = promptDialog.Value;
			};

			WTelegram.Helpers.Log = (int logLevel, string log) => { if (logLevel > (int)Microsoft.Extensions.Logging.LogLevel.Debug) Console.WriteLine(log); };
			_client = new(apiId, apiHash);
			while ((await _client.Login(loginInfo)) is string need)
			{
				promptDialog.Message = $"Введите {need}";
				await this.OpenModal(promptDialog);
			}

			LoginEvent?.Invoke(this.GetType(), _client);
			return $"Вы успешно авторизовались как {_client.User.first_name}";
		}

		public delegate void LoginEventHandler(Type? type, WTelegram.Client client);
		public static event LoginEventHandler? LoginEvent;

		public override async Task Render(MessageResult message)
		{
			string? text;
			try
			{
				text = await this.RenderAsync(message);
			}
			catch (Exception e)
			{
				text = e.Message;
			}

			if (!string.IsNullOrEmpty(text))
				await this.Device.Send(text.Replace("_", "\\_"));
		}
	}
}