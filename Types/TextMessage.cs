﻿using Telegram.Bot.Types.Enums;
using TelegramBotBase.Form;

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
		public ButtonForm? Buttons;
		public ParseMode? ParseMode = null;
		public FormWithArgs NavigateTo;
		public int? ReplyToMessageId;

		public TextMessage NavigateToStart(params object[] args)
		{
			FormBase form = GetStartForm?.Invoke() ?? throw new NullReferenceException();
			this.NavigateTo = new(form, args);
			return this;
		}
	}
}