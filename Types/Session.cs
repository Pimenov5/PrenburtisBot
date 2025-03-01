﻿using System.Text.Json;

namespace PrenburtisBot.Types
{
	internal static class Session
	{
		private static Dictionary<string, Dictionary<string, string>> _types = [];

		public static string? Path;

		public static void Set(Type type, string key, string value)
		{
			if (!_types.ContainsKey(type.Name))
				_types.Add(type.Name, []);

			if (!_types[type.Name].TryAdd(key, value))
				_types[type.Name][key] = value;
		}

		public static string? Get(Type type, string key)
		{
			return _types.TryGetValue(type.Name, out Dictionary<string, string>? dictionary) && dictionary.TryGetValue(key, out string? result) ? result : null;
		}

		public static void Write()
		{
			if (string.IsNullOrEmpty(Path))
				throw new NullReferenceException($"Невозможно записать данные сессии, т.к. не задано значение свойства {nameof(Path)}");
			using FileStream fileStream = File.Exists(Path) ? File.OpenWrite(Path) : File.Create(Path);
			using Utf8JsonWriter writer = new(fileStream);

			JsonSerializer.Serialize(writer, _types);
			writer.Flush();
		}

		public static bool Read()
		{
			if (string.IsNullOrEmpty(Path))
				throw new NullReferenceException($"Невозможно прочитать данные сессии, т.к. не задано значение свойства {nameof(Path)}");
			if (File.Exists(Path))
			{
				using FileStream fileStream = File.OpenRead(Path);
				_types = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(fileStream) ?? _types;
				return true;
			}
			else
				return false;
		}
	}
}