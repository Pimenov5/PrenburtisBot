using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal static class FormBaseExtensions
	{
		public static string[] GetBotCommandParameters(this FormBase formBase, MessageResult messageResult)
		{
			List<string> botCommandParameters = messageResult.BotCommandParameters.Count > 0 ? messageResult.BotCommandParameters : [..(!string.IsNullOrEmpty(messageResult.BotCommand)
				&& messageResult.Command.Contains(messageResult.BotCommand) ? messageResult.Command.Replace(messageResult.BotCommand, string.Empty) : messageResult.Command).Split(' ')];
			botCommandParameters.RemoveAll((string value) => string.IsNullOrEmpty(value));

			if (botCommandParameters.Count > 0 && botCommandParameters[0].Equals(formBase.GetType().Name, StringComparison.OrdinalIgnoreCase))
				botCommandParameters.RemoveAt(0);

			return [..botCommandParameters];
		}
	}
}