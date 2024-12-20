using Telegram.Bot.Types.Enums;
using TelegramBotBase.Form;

namespace PrenburtisBot.Types
{
	internal readonly struct FormWithArgs(FormBase form, params object[] args)
	{
		public readonly FormBase Form = form;
		public readonly object[] Args = args;
	}

	internal class TextMessage(string text)
	{
		public static Func<FormBase>? GetStartForm;

		public readonly string Text = text;
		public ButtonForm? Buttons;
		public ParseMode? ParseMode = null;
		public FormWithArgs NavigateTo;

		public TextMessage NavigateToStart(params object[] args)
		{
			FormBase form = GetStartForm?.Invoke() ?? throw new NullReferenceException();
			this.NavigateTo = new(form, args);
			return this;
		}
	}
}