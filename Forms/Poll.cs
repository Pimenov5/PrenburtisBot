using PrenburtisBot.Attributes;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBotBase.Base;
using TelegramBotBase.Form;

namespace PrenburtisBot.Forms
{
    [GroupAdminCommand]
    internal class Poll : GroupForm
    {
        public static string Description => "Создать опрос для переклички";

        public override async Task Render(MessageResult message)
        {
            Exception? exception = null;
            try
            {
                Telegram.Bot.Types.Message pollMessage = await Device.Api(async (botClient) => await botClient.SendPollAsync(Device.DeviceId,
                    $"Перекличка на волейбол ЗАВТРА ({DateTime.Today.AddDays(1).ToString("dddd", CultureInfo.GetCultureInfo("ru-RU"))})",
                    ["Иду", "Не иду - уступаю своё место", "👀"], isAnonymous: false, type: PollType.Regular, allowsMultipleAnswers: false));

                await Device.Api(async (botClient) => await botClient.PinChatMessageAsync(Device.DeviceId, pollMessage.MessageId));
            }
            catch (Exception e)
            {
                exception = e;
            }

            await NavigateTo(new Start());
            if (exception is not null)
                throw exception;
        }
    }
}