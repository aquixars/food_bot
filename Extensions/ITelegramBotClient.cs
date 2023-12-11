using fobot.POCOs;
using food_bot.POCOs;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using static fobot.GlobalVariables;

namespace fobot.Extensions;

public static class ITelegramBotClientExtensions
{
    public static async Task HandleMenuPageClick(this ITelegramBotClient botClient, CommunicationModel model)
    {
        List<InlineKeyboardButton[]> rows = [];

        foreach (var dishType in dishesTypesCache.Where(dt => dt.IsShowInMainMenu == 1).ToList())
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(dishType.GetDishTypeButtonText(), $"{dishType.GetClickIdentifier()}/1")]);
        }

        if (!string.IsNullOrWhiteSpace(model.Text))
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("Перейти к оформлению", goToOrderCallback)]);
        }

        InlineKeyboardMarkup inlineKeyboard = new(rows);

        if (model.isEditOldMessage)
        {
            await botClient.EditMessageTextAsync(
                chatId: model.TelegramMessage.Chat.Id,
                model.TelegramMessage.MessageId,
                text: string.IsNullOrWhiteSpace(model.Text) ? "Что будем заказывать?" : $"<i>Твой заказ:{model.Text}</i>\nЧто добавим в заказ?",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: model.CancellationToken,
                replyMarkup: inlineKeyboard);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: model.TelegramMessage.Chat.Id,
                text: string.IsNullOrWhiteSpace(model.Text) ? "Что будем заказывать?" : $"<i>Твой заказ:{model.Text}</i>\nЧто добавим в заказ?",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: model.CancellationToken,
                replyMarkup: inlineKeyboard);
        }
    }

    public static async Task HandleCartClearClick(this ITelegramBotClient botClient, CommunicationModel model)
    {
        List<InlineKeyboardButton[]> rows = [];

        foreach (var dishType in dishesTypesCache.Where(dt => dt.IsShowInMainMenu == 1).ToList())
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(dishType.GetDishTypeButtonText(), $"{dishType.GetClickIdentifier()}/1")]);
        }

        InlineKeyboardMarkup inlineKeyboard = new(rows);

        await botClient.EditMessageTextAsync(
            model.TelegramMessage.Chat.Id,
            model.TelegramMessage.MessageId,
            text: $"Корзина очищена.\nЧто будем заказывать?",
            replyMarkup: inlineKeyboard,
            cancellationToken: model.CancellationToken);
    }

    public static async Task HandleGoToOrderPageClick(this ITelegramBotClient botClient, CommunicationModel model)
    {
        InlineKeyboardMarkup inlineKeyboard = new([
                [InlineKeyboardButton.WithCallbackData("Очистить корзину", clearCartCallback)],
            [InlineKeyboardButton.WithCallbackData("Отправить заказ", makeOrderCallback)]]);

        await botClient.EditMessageTextAsync(
                    chatId: model.TelegramMessage.Chat.Id,
                    model.TelegramMessage.MessageId,
                    text: $"Твой заказ:{model.Text}",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: model.CancellationToken);
    }

    public static async Task HandleOrderPageClick(this ITelegramBotClient botClient, CommunicationModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Text))
        {
            InlineKeyboardMarkup inlineKeyboard = new([[InlineKeyboardButton.WithCallbackData("Перейти к меню", backToMenuCallback)]]);

            await botClient.SendTextMessageAsync(
                        chatId: model.TelegramMessage.Chat.Id,
                        text: "В твоей корзине пока ничего нет!",
                        replyMarkup: inlineKeyboard,
                        cancellationToken: model.CancellationToken);
        }
        else
        {
            InlineKeyboardMarkup inlineKeyboard = new([
                [InlineKeyboardButton.WithCallbackData("Очистить корзину", clearCartCallback)],
                [InlineKeyboardButton.WithCallbackData("Отправить заказ", makeOrderCallback)]]);

            await botClient.SendTextMessageAsync(
                        chatId: model.TelegramMessage.Chat.Id,
                        text: $"Твой заказ:{model.Text}",
                        replyMarkup: inlineKeyboard,
                        cancellationToken: model.CancellationToken);
        }
    }

    public static async Task HandleText(this ITelegramBotClient botClient, CommunicationModel model, bool isShowAdminButtons = false)
    {
        List<KeyboardButton[]> rows = [];

        rows.Add([currentFoodButtonText, myOrderButtonText]);
        if (isShowAdminButtons)
        {
            rows.Add([todayOrdersButtonText, unconfirmedOrdersButtonText]);
        }

        ReplyKeyboardMarkup inlineKeyboard = new(rows) { ResizeKeyboard = true, IsPersistent = true };

        await botClient.SendTextMessageAsync(
            chatId: model.TelegramMessage.Chat.Id,
            text: "Чем могу помочь?\r\nВыберите нужное действие внизу экрана!",
            replyMarkup: inlineKeyboard,
            cancellationToken: model.CancellationToken);
    }
    public static async Task HandleMakeOrderClick(this ITelegramBotClient botClient, CommunicationModel model)
    {
        await botClient.EditMessageTextAsync(
            chatId: model.TelegramMessage.Chat.Id,
            model.TelegramMessage.MessageId,
            text: model.Text,
            cancellationToken: model.CancellationToken,
            replyMarkup: new([[InlineKeyboardButton.WithCallbackData("Вернуться к меню", backToMenuCallback)]]),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
    }

    public static async Task HandleGetUnconfirmedOrders(this ITelegramBotClient botClient, CommunicationModel model, List<UnconfirmedOrderModel> unconfirmedOrders)
    {
        List<InlineKeyboardButton[]> rows = [];

        foreach (var order in unconfirmedOrders)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData($"От {order.ClientName} на {order.OrderSumm}", $"{dummyCallback}/confirm/{order.OrderId}")]);
        }

        InlineKeyboardMarkup inlineKeyboard = new(rows);

        if (model.isEditOldMessage)
        {
            await botClient.EditMessageTextAsync(
                chatId: model.TelegramMessage.Chat.Id,
                model.TelegramMessage.MessageId,
                text: rows.Any() ? model.Text : $"Неподтверждённых заказов нет",
                cancellationToken: model.CancellationToken,
                replyMarkup: inlineKeyboard);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: model.TelegramMessage.Chat.Id,
                text: rows.Any() ? model.Text : $"Неподтверждённых заказов нет",
                cancellationToken: model.CancellationToken,
                replyMarkup: inlineKeyboard);
        }
    }
}