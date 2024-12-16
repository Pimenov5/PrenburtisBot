using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Узнать номер своей команды")]
	internal class Join : BotCommandFormBase
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

			string firstName = (await this.Device.GetChatUser(userId)).User.FirstName;
			Player player = Users.GetPlayer(userId, firstName);
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

					await this.NavigateTo(confirmDialog);
					return null;
				}
				else if (!isConfirmed ?? throw new NullReferenceException())
				{
					await this.Device.Send("Вы отменили повторное присоединение к площадке");
					await this.NavigateTo(new Start());
					return null;
				}
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
				if (player.FirstName != firstName)
					text += Environment.NewLine + $"Пожалуйста, обратитесь к администратору для обновления вашего имени на {firstName}";
			}

			ButtonForm? buttonForm = teams.Any((string? value) => value is not null) ? new() : null;
			buttonForm?.AddButtonRow(new ButtonBase("👀", new CallbackData(nameof(Players), courtId).Serialize()),
				new ButtonBase("❌", new CallbackData(nameof(Leave), courtId).Serialize()));

			return new(text) { Buttons = buttonForm };
		}
	}
}