using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotBase.Form;
using TelegramBotBase.Markdown;

namespace PrenburtisBot.Types
{
	internal readonly struct FormWithArgs(FormBase form, params object[] args)
	{
		public readonly FormBase Form = form;
		public readonly object[] Args = args;
	}

	internal enum TextMessageKind { Unknown, Error }

	internal class TextMessage(string text, TextMessageKind messageKind = TextMessageKind.Unknown)
	{
		public static Func<FormBase>? GetStartForm;

		public string Text = text;
		public TextMessageKind Kind = messageKind;
		public ReplyMarkup? ReplyMarkup;
		public ParseMode ParseMode;
		public FormWithArgs NavigateTo;
		public int? ReplyToMessageId;
		public LinkPreviewOptions? LinkPreviewOptions;

		public TextMessage NavigateToStart(params object[] args)
		{
			FormBase form = GetStartForm?.Invoke() ?? throw new NullReferenceException();
			this.NavigateTo = new(form, args);
			return this;
		}

		public TextMessage SetErrorKind(ParseMode parseMode = ParseMode.Markdown)
		{
			this.Kind = TextMessageKind.Error;
			this.Text = this.Text.Bold();
			this.ParseMode = parseMode;
			return this;
		}
	}

	internal class ErrorTextMessage : TextMessage
	{
		public ErrorTextMessage(string text, ParseMode parseMode = ParseMode.Markdown) : base(text, TextMessageKind.Error)
		{
			this.SetErrorKind(parseMode);
		}
	}
}