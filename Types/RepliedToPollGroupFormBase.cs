using PrenburtisBot.Forms;
using Telegram.Bot;
using TelegramBotBase.Base;

namespace PrenburtisBot.Types
{
	internal abstract class RepliedToPollGroupFormBase : BotCommandGroupFormBase
	{
		public static WTelegram.Client? TelegramClient = null;

		protected abstract TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, params string[] args);
		protected virtual TextMessage GetTextMessage(long userId, IReadOnlyCollection<Player> players, MessageResult message, params string[] args) => GetTextMessage(userId, players, args);

		public async Task<TextMessage> RenderAsync(MessageResult message)
		{
			if (message.Message.ReplyToMessage is not Telegram.Bot.Types.Message repliedMessage || repliedMessage.Poll is not Telegram.Bot.Types.Poll poll || poll.IsAnonymous
				|| poll.AllowsMultipleAnswers || poll.Options.Length < 1 || poll.Options[0].Text != SendPoll.PLAYER_JOINED)
			{
				return new($"Команда должна вызываться в ответ на не анонимный опрос с первым вариантом ответа \"{SendPoll.PLAYER_JOINED}\"");
			}

			if (TelegramClient is null)
				return new("Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались");

			IReadOnlyCollection<Player> players = await TelegramClient.GetPlayersFromPoll(repliedMessage, [SendPoll.PLAYER_JOINED_BYTE]);

			long userId = message.Message.From?.Id ?? throw new NullReferenceException();
			string[] args = message.BotCommandParameters.ToArray();
			if (args.Length > 0 && args[^1].StartsWith('@') && (await this.API.GetMe()).Username is string botUsername && args[^1].Equals('@' + botUsername))
				Array.Resize(ref args, args.Length - 1);

			TextMessage textMessage = this.GetTextMessage(userId, players, message, args);
			textMessage.ReplyToMessageId ??= repliedMessage.MessageId;
			return textMessage;
		}
	}
}