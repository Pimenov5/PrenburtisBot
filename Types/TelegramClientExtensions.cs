using WTelegram;
using TL;

namespace PrenburtisBot.Types
{
	internal static class TelegramClientExtensions
	{
		public static async Task<IReadOnlyCollection<Player>> GetPlayersFromPoll(this Client client, Telegram.Bot.Types.Message message, byte[] option)
		{
			if (message.Poll is not Telegram.Bot.Types.Poll poll)
				throw new NullReferenceException($"Сообщение с ID {message.MessageId} не содержит опрос");

			Messages_VotesList? votes = null;
			try
			{
				Messages_Chats chats = await client.Messages_GetAllChats();
				long chatId = message.Chat.Id.ToString().StartsWith("-100") ? long.Parse(message.Chat.Id.ToString().Remove(0, 4)) : message.Chat.Id;
				if (!chats.chats.TryGetValue(chatId, out ChatBase? chatBase) || chatBase is not Channel channel)
					throw new InvalidOperationException($"Не удалось найти {message.Chat.Type} с ID {chatId}");

				votes = await client.Messages_GetPollVotes(new InputChannel(channel.id, channel.access_hash), message.MessageId, option: option);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			if (votes is null)
				throw new NullReferenceException($"Не удалось получить список проголосовавших в опросе \"{poll.Question}\"");

			List<Player> players = new(votes.count);
			foreach (MessagePeerVoteBase vote in votes.votes)
			{
				User user = votes.users[vote.Peer.ID];
				players.Add(Users.GetPlayer(vote.Peer.ID, user.first_name, user.username));
			}

			return players;
		}
	}
}