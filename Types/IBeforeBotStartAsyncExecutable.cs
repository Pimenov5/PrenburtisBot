namespace PrenburtisBot.Types
{
	internal interface IBeforeBotStartAsyncExecutable
	{
		public Task<string?> ExecuteAsync();
	}
}