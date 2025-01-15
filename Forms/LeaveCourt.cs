using PrenburtisBot.Attributes;
using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	[BotCommand("Выйти из всех команд на площадке")]
	internal class LeaveCourt : BotCommandFormBase
	{
		public TextMessage Render(long userId) => Render(userId, null, null);
		public TextMessage Render(long userId, string? courtId) => Render(userId, courtId, null);
		public TextMessage Render(long userId, string? courtId, string? confirmation)
		{
			Court court = Courts.GetById(ref courtId, userId);

			bool? isConfirmed = !string.IsNullOrEmpty(confirmation) && bool.TryParse(confirmation, out bool value) ? value : null;
			switch (isConfirmed) {
				case null:
					ConfirmDialog confirmDialog = new("Вы точно хотите покинуть эту площадку?" + Environment.NewLine + "Вы не сможете присоединится к ней ещё раз",
							new ButtonBase("Да", bool.TrueString), new ButtonBase("Нет", bool.FalseString)) { AutoCloseOnClick = false };
					confirmDialog.ButtonClicked += async (Object? sender, ButtonClickedEventArgs eventArgs) =>
					{
						await confirmDialog.NavigateTo(this, courtId, eventArgs.Button.Value);
					};

					return new TextMessage(string.Empty) { NavigateTo = new(confirmDialog) };

				case true:
					int[] indexes = court.RemovePlayer(userId);
					string[] teams = new string[indexes.Length];
					for (int i = 0; i < teams.Length; i++)
						teams[i] = (indexes[i] + 1).ToString() + court.Teams[indexes[i]].FormatName();

					return new TextMessage(teams.Length switch
					{
						0 => "Вы ещё не присоединились ни к одной команде на площадке",
						1 => $"Вы вышли из команды #{teams[0]}",
						2 => $"Вы вышли из команды #{teams[0]} и #{teams[1]}",
						_ => "Вы вышли из команд под номерами: " + new StringBuilder().AppendJoin(", ", teams)
					}).NavigateToStart();

				case false:
					return new TextMessage("Вы отменили выход из команд на площадке").NavigateToStart(Start.SET_QUIET);

			}
		}
	}
}