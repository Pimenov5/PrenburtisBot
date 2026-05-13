using WTelegram;
using TL;

namespace PrenburtisBot.Types
{
	internal static class TelegramClientExtensions
	{
		public static async Task<InputChannel> CreateInputChannelAsync(this Client client, Telegram.Bot.Types.Chat chat)
		{
			Messages_Chats chats = await client.Messages_GetAllChats();
			long chatId = chat.Id.ToString().StartsWith("-100") ? long.Parse(chat.Id.ToString()[4..]) : chat.Id;
			if (!chats.chats.TryGetValue(chatId, out ChatBase? chatBase) || chatBase is not Channel channel)
				throw new InvalidOperationException($"Не удалось найти {chat.Type} с ID {chatId}");

			return new(channel.id, channel.access_hash);
		}

		public static async Task<IReadOnlyCollection<Player>> GetPlayersFromPollAsync(this Client client, Telegram.Bot.Types.Message message, int optionIndex)
		{
			Dictionary<int, List<Player>> dictionary = await GetPlayersFromPollAsync(client, message);
			return dictionary[optionIndex];
		}

		public static async Task<Dictionary<int, List<Player>>> GetPlayersFromPollAsync(this Client client, Telegram.Bot.Types.Message message)
		{
			if (message.Poll is not Telegram.Bot.Types.Poll poll)
				throw new NullReferenceException($"Сообщение с ID {message.MessageId} не содержит опрос");

			Messages_VotesList? votes = null;
			try
			{
				InputChannel inputChannel = await client.CreateInputChannelAsync(message.Chat);
				votes = await client.Messages_GetPollVotes(inputChannel, message.MessageId);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			if (votes is null)
				throw new NullReferenceException($"Не удалось получить список проголосовавших в опросе \"{poll.Question}\"");

			Dictionary<int, List<Player>> players = new(votes.count);
			foreach (MessagePeerVote vote in votes.votes.Cast<MessagePeerVote>())
			{
				TL.User user = votes.users[vote.Peer.ID];
				int optionIndex = int.Parse(vote.option);
				if (!players.ContainsKey(optionIndex))
					players.Add(optionIndex, []);

				players[optionIndex].Add(Users.GetPlayer(vote.Peer.ID, user.first_name, user.username));
			}

			return players;
		}
	}
}