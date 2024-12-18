using Telegram.Bot.Types.Enums;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal class TextMessage(string text)
	{
		public readonly string Text = text;
		public ButtonForm? Buttons;
		public ParseMode? ParseMode = null;
	}
}