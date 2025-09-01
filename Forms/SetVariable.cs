using PrenburtisBot.Attributes;
using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
	[BotCommandChat("Задать переменную среды", "BOT_OWNER_CHAT_ID")]
	internal class SetVariable : BotCommandFormBase
	{
		private string? _variable;

		public static string Render() => "Введите имя переменной среды";
		public string Render(string value)
		{
			if (_variable is null)
			{
				_variable = value;
				return "Введите значение переменной среды";
			}

			string variable = _variable;
			_variable = null;
			return Render(variable, value);
		}
		public static string Render(string variable, string? value)
		{
			if (value == "null")
				value = null;
			Environment.SetEnvironmentVariable(variable, value);
			return $"{variable} = {Environment.GetEnvironmentVariable(variable)}";
		}
	}
}