using PrenburtisBot.Attributes;
using PrenburtisBot.Forms;
using Telegram.Bot;
using TelegramBotBase.Base;

namespace PrenburtisBot.Types
{
	[NeededTelegramClient]
	internal abstract class RepliedToPollGroupFormBase : BotCommandGroupFormBase
	{
		private static WTelegram.Client? _telegramClient = null;

		public static WTelegram.Client TelegramClient { set { _telegramClient = value; } }

		protected abstract TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args);
		protected virtual IReadOnlyList<TextMessage> GetTextMessages(long userId, IReadOnlyCollection<Player> players, params string[] args) => [GetTextMessage(userId, players, args)];

		public const string MUST_REMOVE_POLL = "removePoll";

		public async Task<IReadOnlyList<TextMessage>> RenderAsync(MessageResult message)
		{
			if (message.Message.ReplyToMessage is not Telegram.Bot.Types.Message repliedMessage || repliedMessage.Poll is not Telegram.Bot.Types.Poll poll || poll.IsAnonymous
				|| poll.AllowsMultipleAnswers || poll.Options.Length < 1 || poll.Options[0].Text != SendPoll.PLAYER_JOINED)
			{
				return [new($"Команда должна вызываться в ответ на не анонимный опрос с первым вариантом ответа \"{SendPoll.PLAYER_JOINED}\"")];
			}

			if (_telegramClient is null)
				return [new("Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались")];

			IReadOnlyCollection<Player> players = await _telegramClient.GetPlayersFromPollAsync(repliedMessage, 0);

			List<string> parameters = message.BotCommandParameters;
			if (parameters.Count > 0 && parameters[^1].StartsWith('@') && (await this.API.GetMe()).Username is string botUsername && parameters[^1].Equals('@' + botUsername))
				parameters.RemoveAt(parameters.Count - 1);
			int index = parameters.IndexOf(MUST_REMOVE_POLL);
			if (index >= 0)
				parameters.RemoveAt(index);

			long userId = message.Message.From?.Id ?? throw new NullReferenceException();
			IReadOnlyList<TextMessage> textMessages = this.GetTextMessages(userId, players, [..parameters]);
			foreach (TextMessage textMessage in textMessages)
				textMessage.ReplyToMessageId ??= repliedMessage.MessageId;

			if (index >= 0)
				await this.Device.DeleteMessage(repliedMessage.MessageId);

			return textMessages;
		}
	}
}