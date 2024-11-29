using PrenburtisBot.Types;

namespace PrenburtisBot.Forms
{
	internal class Start : LinkedForm
	{
		protected override async Task<string?> RenderAsync(params string[] args) => this.Device.IsGroup ? null : "Введите команду или выберите из меню";
	}
}