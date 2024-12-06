using Telegram.Bot.Types.Enums;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal class TextMessage(string text)
	{
		public readonly string Text = text;
		public ButtonForm? Buttons;
		public int ReplyTo = default;
		public bool DisableNotification = false;
		public ParseMode ParseMode = ParseMode.Markdown;
		public bool MarkdownV2AutoEscape = true;
	}
}