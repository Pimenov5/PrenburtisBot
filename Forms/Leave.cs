using PrenburtisBot.Types;
using System.Text;
using TelegramBotBase.Args;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
	internal class Leave : LinkedForm
	{
		protected override async Task<string?> RenderAsync(params string[] args)
		{
			string? courtId = args.Length >= 1 ? args[0] : null;
			Court court = Courts.GetById(courtId);

			bool? isConfirmed = 1 <= args.Length && args.Length <= 2 && bool.TryParse(args[^1], out bool value) ? value : null;
			switch (isConfirmed) {
				case null:
					ConfirmDialog confirmDialog = new("Вы точно хотите покинуть эту площадку?" + Environment.NewLine + "Вы не сможете присоединится к ней ещё раз",
							new ButtonBase("Да", bool.TrueString), new ButtonBase("Нет", bool.FalseString)) { AutoCloseOnClick = false };
					confirmDialog.ButtonClicked += async (Object? sender, ButtonClickedEventArgs eventArgs) =>
					{
						await confirmDialog.NavigateTo(this, courtId, eventArgs.Button.Value);
					};

					await this.NavigateTo(confirmDialog);
					return null;

				case true:
					int[] indexes = court.RemovePlayer(this.Device.DeviceId);
					string[] teams = new string[indexes.Length];
					for (int i = 0; i < teams.Length; i++)
						teams[i] = (indexes[i] + 1).ToString() + court.Teams[indexes[i]].FormatName();

					string text = teams.Length switch
					{
						0 => "Вы ещё не присоединились ни к одной команде на площадке",
						1 => $"Вы вышли из команды #{teams[0]}",
						2 => $"Вы вышли из команды #{teams[0]} и #{teams[1]}",
						_ => "Вы вышли из команд под номерами: " + new StringBuilder().AppendJoin(", ", teams)
					};

					return text;

				case false:
					await this.Device.Send("Вы отменили выход из команд на площадке");
					await this.NavigateTo(new Start());
					return null;

			}
		}

		public static string Description => "Выйти из всех команд на площадке";
	}
}