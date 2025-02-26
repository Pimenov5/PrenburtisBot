namespace PrenburtisBot.Attributes
{
	[AttributeUsage(AttributeTargets.Class)]
	internal class BeforeBotStartExecutableAttribute(string methodName) : Attribute
	{
		public readonly string MethodName = methodName;
		public static string GetPath(string variable)
		{
			if (Environment.GetEnvironmentVariable(variable) is not string path)
				throw new Exception($"Отсутствует значение переменной окружения {variable}");
			if (!File.Exists(path))
				throw new Exception($"Не существует файла {path}");
			return path;
		}
	}
}