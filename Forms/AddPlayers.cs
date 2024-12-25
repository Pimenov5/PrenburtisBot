using PrenburtisBot.Types;
using PrenburtisBot.Attributes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using TelegramBotBase.Form;
using TL;
using Telegram.Bot;

namespace PrenburtisBot.Forms
{
	[BotCommand("Добавить к площадке игроков из опроса", BotCommandScopeType.AllChatAdministrators)]
	internal class AddPlayers : GroupForm
	{
		private async Task<string?> RenderAsync(MessageResult message)
		{
			if (message.Message.ReplyToMessage is not Telegram.Bot.Types.Message repliedMessage || repliedMessage.Poll is not Telegram.Bot.Types.Poll poll || poll.IsAnonymous
				|| poll.AllowsMultipleAnswers || poll.Options.Length < 1 || poll.Options[0].Text != SendPoll.PLAYER_JOINED)
			{
				return $"Команда должна вызываться в ответ на не анонимный опрос с первым вариантом ответа \"{SendPoll.PLAYER_JOINED}\"";
			}

			string? courtId = message.BotCommandParameters.Count >= 1 ? message.BotCommandParameters[0] : null;
			Court court;
			try
			{
				court = Courts.GetById(ref courtId, message.Message.From?.Id);
			}
			catch
			{
				if (string.IsNullOrEmpty(courtId))
					return "Вы не указали идентификатор площадки";
				else if (!uint.TryParse(courtId, out uint value))
					return $"\"{courtId}\" не является валидным идентификатором площадки";
				else
					return $"Не удалось найти площадку с идентификатором {courtId}";
			}

			if (TelegramClient is null)
				return "Невозможно получить список проголосовавших в опросе, т.к. вы ещё не авторизовались";

			Messages_VotesList? votes = null;
			try
			{
				Messages_Chats chats = await TelegramClient.Messages_GetAllChats();
				long chatId = repliedMessage.Chat.Id.ToString().StartsWith("-100") ? long.Parse(repliedMessage.Chat.Id.ToString().Remove(0, 4)) : repliedMessage.Chat.Id;
				if (!chats.chats.TryGetValue(chatId, out ChatBase? chatBase) || chatBase is not Channel channel)
					return $"Не удалось найти {repliedMessage.Chat.Type} с ID {chatId}";

				votes = await TelegramClient.Messages_GetPollVotes(new InputChannel(channel.id, channel.access_hash), repliedMessage.MessageId, option: [SendPoll.PLAYER_JOINED_BYTE]);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			if (votes is null)
				return $"Не удалось получить список проголосовавших в опросе \"{poll.Question}\"";
			else if (votes.count == 0)
				return $"Ещё никто не проголосовал в опросе \"{poll.Question}\"";

			int count = 0;
			foreach (Team team in court.Teams)
				count += team.PlayerCount;

			long maxCount = court.Teams.Length * court.TeamMaxPlayerCount;
			if (votes.count > maxCount - count)
				return $"Недостаточно свободных мест на площадке ({maxCount - count}) для добавления игроков из опроса ({votes.count})";

			List<Player> players = new(votes.count);
			foreach (MessagePeerVoteBase vote in votes.votes)
			{
				Player player = Users.GetPlayer(vote.Peer.ID, votes.users[vote.Peer.ID].first_name);
				player.Username = votes.users[vote.Peer.ID].username;
				players.Add(player);
			}

			uint?[] teams = court.AddPlayers(players);
			count = 0;
			foreach (uint? index in teams)
				count = index is null ? count + 1 : count;

			if (count != 0)
				await this.Device.Send($"Не удалось добавить {count} из {teams.Length} игроков");

			await this.NavigateTo(new CourtPlayers(), courtId);

			return null;
		}

		public static WTelegram.Client? TelegramClient = null;

		public override async Task Render(MessageResult message)
		{
			string? text;
			try
			{
				text = await this.RenderAsync(message);
			}
			catch (Exception e)
			{
				text = e.Message;
			}

            if (!string.IsNullOrEmpty(text))
            {
				await this.Device.Api(async (ITelegramBotClient botClient) => await botClient.SendTextMessageAsync(this.Device.DeviceId, text,
					message.Message.Chat.IsForum ?? false ? message.Message.MessageThreadId : null));
				await this.NavigateTo(new Start());
            }
        }
	}
}