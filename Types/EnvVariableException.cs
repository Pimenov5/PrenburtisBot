namespace PrenburtisBot.Types
{
	internal class EnvVariableException(string name, string format = "В переменных окружения отсутствует значение {0}") : NullReferenceException(string.Format(format, name))
	{
	}
}