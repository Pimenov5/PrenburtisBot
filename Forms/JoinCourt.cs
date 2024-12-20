using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Присоединяться к команде на площадке")]
	internal class JoinCourt : BotCommandFormBase
	{
		protected override async Task<TextMessage?> RenderAsync(long userId, params string[] args)
		{
			bool? isConfirmed = null;
			string?[] teams = new string?[1];
			if (args.Length >= 2)
			{
				if (int.TryParse(args[1], out int intValue))
				{
					if (intValue <= 0)
						return new("Число присоединений к площадке должно быть больше нуля");
					else
						Array.Resize(ref teams, intValue);
				}

				if (bool.TryParse(args[1], out bool boolValue) || (args.Length == 3 && bool.TryParse(args[2], out boolValue)))
					isConfirmed = boolValue;
			}

			string? courtId = args.Length >= 1 ? args[0] : null;
			Court court = Courts.GetById(ref courtId, userId);

			Telegram.Bot.Types.User user = (await this.Device.GetChatUser(userId)).User;
			Player player = Users.GetPlayer(userId, user.FirstName);
			player.Username = user.Username;

			if (court is RankedCourt && player.Rank == default && court.ContainsPlayer(player.UserId))
			{
				if (isConfirmed is null)
				{
					ConfirmDialog confirmDialog = new("Вы пытаетесь повторно присоединится к площадке, учитывающей ранги игроков. Продолжить?",
						new("Да", bool.TrueString), new("Нет", bool.FalseString))
					{ AutoCloseOnClick = false };
					confirmDialog.ButtonClicked += async (object? sender, ButtonClickedEventArgs args) =>
					{
						await this.NavigateTo(this, courtId, teams.Length, args.Button.Value);
					};

					return new TextMessage(string.Empty) { NavigateTo = new(confirmDialog) };
				}
				else if (!isConfirmed ?? throw new NullReferenceException())
					return new TextMessage("Вы отменили повторное присоединение к площадке").NavigateToStart();
			}

			for (int i = 0; i < teams.Length; i++) 
				teams[i] = court.AddPlayer(player) is uint teamIndex ? $"#{teamIndex + 1}" + court.Teams[(int)teamIndex].FormatName() : null;

			string text = teams.Length switch
			{
				1 => teams[0] is null ? "Нет свободных мест в командах на площадке" : "Вы были добавлены в команду " + teams[0],
				2 => teams[0] is null && teams[0] == teams[1] ? "Нет свободных мест в командах на площадке" : teams[1] is null ? "Вы были добавлены только в команду " + teams[0]
					: "Вы были добавлены в команду " + teams[0] + " и " + teams[1],
				_ => "Вы были добавлены в команды: " + new StringBuilder().AppendJoin('_', teams).ToString().TrimEnd('_').Replace("_", ", ")
			};

			if (court is RankedCourt) {
				if (player.Rank == default)
					text += Environment.NewLine + "На площадке учитываются ранги игроков, обратитесь к администратору, чтобы добавить свой";
				if (player.FirstName != user.FirstName)
					text += Environment.NewLine + $"Пожалуйста, обратитесь к администратору для обновления вашего имени на {user.FirstName}";
			}

			ButtonForm? buttonForm = teams.Any((string? value) => value is not null) ? new() : null;
			buttonForm?.AddButtonRow(new ButtonBase("👀", new CallbackData(nameof(CourtPlayers), courtId).Serialize()),
				new ButtonBase("❌", new CallbackData(nameof(LeaveCourt), courtId).Serialize()));

			return new TextMessage(text) { Buttons = buttonForm }.NavigateToStart(Start.SET_QUIET);
		}
	}
}