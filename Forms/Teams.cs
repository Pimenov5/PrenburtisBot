using Telegram.Bot.Types.ReplyMarkups;
using PrenburtisBot.Types;
using TelegramBotBase.Form;
using PrenburtisBot.Attributes;

namespace PrenburtisBot.Forms
{
	[BotCommand("Распределить игроков по командам")]
	internal class Teams : BotCommandFormBase
	{
		protected bool? _isRanked = null;
		protected uint _teamCount = default, _teamMaxPlayerCount = default;

		protected async Task<bool> CanCreateCourtAsync(params string[] args)
		{
			string GetText(out string[] buttons)
			{
				buttons = new string[3];
				uint prevValue = _teamCount;
				_teamCount = _teamCount != default ? _teamCount : 1 <= args.Length && args.Length <= 3 && uint.TryParse(args[0], out _teamCount) && _teamCount >= 2 ? _teamCount : default;
				if (_teamCount == default)
				{
					buttons = ["2", "3", "4"];
					return "Введите количество команд (от 2)";
				}
				else if (prevValue != _teamCount && args.Length == 1)
					args[0] = default(uint).ToString();

				_teamMaxPlayerCount = _teamMaxPlayerCount != default ? _teamMaxPlayerCount : (args.Length == 2 || args.Length == 3) && uint.TryParse(args[1], out _teamMaxPlayerCount)
					? _teamMaxPlayerCount : args.Length == 1 && _teamCount != default && uint.TryParse(args[0], out _teamMaxPlayerCount) && _teamMaxPlayerCount >= 2 ? _teamMaxPlayerCount : default;
				if (_teamMaxPlayerCount == default)
				{
					buttons = ["5", "6", "7"];
					return "Введите количество игроков в команде (от 2)";
				}

				return string.Empty;
			}

			string text = GetText(out string[] buttons);
			_isRanked = args.Length == 3 && bool.TryParse(args[2], out bool value) ? value : null;

			if (_teamCount == default || _teamMaxPlayerCount == default)
			{
				List<KeyboardButton> keyboardButtons = new(buttons.Length);
				foreach (string button in buttons)
					keyboardButtons.Add(new KeyboardButton(button));

				await this.Device.Send(text, new ReplyKeyboardMarkup(keyboardButtons) { ResizeKeyboard = true });
			}
			else if (_isRanked is null)
			{
				ConfirmDialog confirmDialog = new("Распределять игроков с учётом их ранга?", new ButtonBase("Да", bool.TrueString), new ButtonBase("Нет", bool.FalseString)) { AutoCloseOnClick = false };
				confirmDialog.ButtonClicked += async (sender, eventArgs) =>
				{
					await confirmDialog.NavigateTo(this, _teamCount, _teamMaxPlayerCount, eventArgs.Button.Value);
				};

				await this.NavigateTo(confirmDialog);
			}
			else
			{
				await this.Device.HideReplyKeyboard();
				return true;
			}

			return false;
		}

		protected override async Task<TextMessage?> RenderAsync(long userId, params string[] args)
		{
			if (!await this.CanCreateCourtAsync(args))
				return null;

			string[] names = Team.Names;
			Random? random = names.Length >= _teamCount ? new() : null;
			List<Team> teams = [];
			for (int i = 0; i < _teamCount; i++)
				teams.Add(new Team(random is null ? null : names[random.Next(names.Length)]));

			Court court = _isRanked ?? throw new NullReferenceException() ? new RankedCourt(userId, teams, _teamMaxPlayerCount)
				: new Court(userId, teams, _teamMaxPlayerCount);
			int courtId = Courts.Add(court);

			ButtonForm buttonForm = new();
			if (!this.Device.IsGroup)
			{
				string value = courtId.ToString();
				buttonForm.AddButtonRow(new ButtonBase("✏️", new CallbackData(nameof(Edit), value).Serialize()),
					new ButtonBase("🔀", new CallbackData(nameof(Shuffle), value).Serialize()),
					new ButtonBase("👀", new CallbackData(nameof(Players), value).Serialize()));
			}

			string text = await Start.GetDeepLinkAsync(this.Client.TelegramClient, typeof(Join), courtId.ToString());
			return new(text) { Buttons = buttonForm };
		}
	}
}