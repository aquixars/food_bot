using fobot.Database;
using fobot.Database.Models;
using fobot.Extensions;
using fobot.Logging;
using fobot.POCOs;
using fobot.Services;
using food_bot.Enums;
using food_bot.POCOs;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static fobot.Extensions.DishExtensions;
using static fobot.Extensions.DishTypesExtensions;
using static fobot.Extensions.ITelegramBotClientExtensions;
using static fobot.GlobalVariables;

namespace fobot;

public class TelegramBackgroundWorker : BackgroundService
{
    private static readonly ILogger _logger = ApplicationLog.Common;

    private static IServiceProvider serviceProvider;

    private readonly string telegramBotToken;
    private static ITelegramBotClient telegramBot;

    public TelegramBackgroundWorker(IServiceProvider _serviceProvider)
    {
        serviceProvider = _serviceProvider;

        telegramBotToken = SecretsReader.ReadSection<string>("TelegramBot:Token");
        telegramBot = new TelegramBotClient(telegramBotToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CacheData();
        await LaunchBot();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private static async Task CacheData()
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        dishesCache = [.. scope.ServiceProvider.GetRequiredService<LocalDBContext>().Dishes];
        dishesTypesCache = [.. scope.ServiceProvider.GetRequiredService<LocalDBContext>().DishTypes];
    }

    private async Task LaunchBot()
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<OrderService>().InitMenuCallbacks();

            var cancellationToken = new CancellationTokenSource().Token;

            telegramBot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, new ReceiverOptions { AllowedUpdates = { }, }, cancellationToken);

            await telegramBot.SetMyCommandsAsync(new List<BotCommand>() {
                new() { Command = settingsCommandText, Description = settingsButtonText },
                new() { Command = historyCommandText, Description = historyButtonText }
            }, new BotCommandScopeDefault(), "ru", cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка в основном потоке!");
        }
    }

    private static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Перехваченное исключение");
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            User sender = (update?.Message?.From) ?? (update?.CallbackQuery?.From);

            if (sender is null)
            {
                return;
            }

            await using var scope = serviceProvider.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<UserService>().SaveUserActivityInfo(sender);

            var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

            var dbUser = db.Clients.FirstOrDefault(u => u.ExternalId == sender.Id);
            var currentAdmin = await scope.ServiceProvider.GetRequiredService<AdminService>().GetActiveAdmin();

            CommunicationModel model = new()
            {
                CancellationToken = cancellationToken,
                Sender = sender
            };

            string senderName = !string.IsNullOrWhiteSpace(dbUser.SystemName) ? dbUser.SystemName : $"{dbUser.UserName} ({dbUser.FirstName} {dbUser.LastName})";

            // кнопки из тела диалога
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery == null || update.CallbackQuery.Data == null || update.CallbackQuery.Message == null)
                    return;

                var callbackQuery = update.CallbackQuery;
                var callbackQueryData = callbackQuery.Data;

                model.TelegramMessage = callbackQuery.Message;

                // забираем номер странички
                int pageNumber = 1;
                if (callbackQueryData.Contains('/'))
                {
                    var slashSegments = callbackQueryData.Split("/");
                    if (int.TryParse(slashSegments[1], out pageNumber))
                    {
                        callbackQueryData = slashSegments[0];
                    }
                }

                var foodCallback = foodCallbacks.SingleOrDefault(fc => callbackQueryData.StartsWith(fc.CallbackFunctionName));
                if (foodCallback is not null) // клик на блюдо
                {
                    int childId = 0;

                    if (callbackQueryData.Contains('.'))
                    {
                        var dotSegments = callbackQueryData.Split(".");
                        _ = int.TryParse(dotSegments[1], out childId);
                        callbackQueryData = dotSegments[0];
                    }

                    var colonSegments = callbackQueryData.Split(":");
                    _ = int.TryParse(colonSegments[0], out int dishTypeId);
                    _ = int.TryParse(colonSegments[1], out int dishId);

                    var parent = dishesCache.SingleOrDefault(d => d.Id == dishId);

                    if (childId != 0) // если выбрали гарнир к блюду
                    {
                        foodCallback.CallbackFunction(dbUser.Id, childId);
                        var child = dishesCache.SingleOrDefault(d => d.Id == childId);
                        var parentDishTypeId = parent.DishTypeId;
                        callbackQueryData = dishesTypesCache.SingleOrDefault(dt => dt.Id == parentDishTypeId).GetClickIdentifier();
                        _logger.LogInformation($"Пользователь [{senderName}] добавил [{parent.Name}]+[{child.Name}] в корзину");
                    }
                    else if (foodCallback.IsFlavoringIncluded)
                    { // если кликнули на блюдо, которое подается с заправкой на выбор
                        callbackQueryData = $"{garnishMenuCallback}:{dishTypeId}:{dishId}:6";
                    }
                    else if (!foodCallback.IsGarnishIncluded) // если блюдо подаётся без гарнира
                    {
                        foodCallback.CallbackFunction(dbUser.Id, 0);
                        callbackQueryData = dishesTypesCache.SingleOrDefault(dt => dt.Id == dishTypeId).GetClickIdentifier();
                        _logger.LogInformation($"Пользователь [{senderName}] добавил [{parent.Name}] в корзину");
                    }
                    else // если кликнули на блюдо, которое подаётся с гарниром
                    {
                        callbackQueryData = $"{garnishMenuCallback}:{dishTypeId}:{dishId}:4";
                    }
                }

                var order = db.Orders.SingleOrDefault(x => x.ClientId == dbUser.Id && x.IsSend == 0);
                List<OrderLine> orderLines = [];
                if (order is not null)
                {
                    orderLines = [.. db.OrderLines.Where(x => x.OrderId == order.Id)];
                }

                if (callbackQueryData.StartsWith("dishType:"))
                {
                    _ = int.TryParse(callbackQueryData.Split(":")[1], out int dishTypeId);

                    string callBack = dishesTypesCache.SingleOrDefault(dt => dt.Id == dishTypeId).GetClickIdentifier();

                    var dishes = dishesCache.Where(d => d.DishTypeId == dishTypeId).OrderBy(d => d.Sort).ToList();

                    List<InlineKeyboardButton[]> rows = [];

                    if (dishes.Count > pageSize) // by pages
                    {
                        foreach (var dish in dishes.Skip(pageSize * (pageNumber - 1)).Take(pageSize))
                        {
                            rows.Add([InlineKeyboardButton.WithCallbackData(dish.GetDishButtonText(), $"{dish.GetClickIdentifier()}/{pageNumber}")]);
                        }

                        var lastPageNumber = Math.Round(Math.Ceiling(a: (double)dishes.Count / pageSize));

                        //if (pageNumber == 1) // первая страница
                        //{
                        //    rows.Add([InlineKeyboardButton.WithCallbackData(nextPageButtonText, $"{callBack}/{pageNumber + 1}")]);
                        //}
                        //else if (pageNumber == lastPageNumber) // последняя страница
                        //{
                        //    rows.Add([InlineKeyboardButton.WithCallbackData(previousPageButtonText, $"{callBack}/{pageNumber - 1}")]);
                        //}
                        //else // всё что между ними
                        //{
                        //    rows.Add([
                        //        InlineKeyboardButton.WithCallbackData(previousPageButtonText, $"{callBack}/{pageNumber - 1}"),
                        //        InlineKeyboardButton.WithCallbackData(nextPageButtonText, $"{callBack}/{pageNumber + 1}"),
                        //    ]);
                        //}

                        rows.Add([
                                InlineKeyboardButton.WithCallbackData(previousPageButtonText, pageNumber == 1 ? dummyCallback : $"{callBack}/{pageNumber - 1}"),
                                InlineKeyboardButton.WithCallbackData($"[{pageNumber}/{lastPageNumber}]", dummyCallback),
                                InlineKeyboardButton.WithCallbackData(nextPageButtonText, pageNumber == lastPageNumber ? dummyCallback : $"{callBack}/{pageNumber + 1}"),
                            ]);
                    }
                    else // just everything
                    {
                        foreach (var dish in dishes)
                        {
                            rows.Add([InlineKeyboardButton.WithCallbackData(dish.GetDishButtonText(), $"{dish.GetClickIdentifier()}/{pageNumber}")]);
                        }
                    }

                    rows.Add([InlineKeyboardButton.WithCallbackData("↩️ К списку блюд", backToMenuCallback)]);

                    InlineKeyboardMarkup inlineKeyboard = new(rows);

                    string userOrderInfo = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(sender.Id)).OrderInfo;
                    string dishTypeName = dishesTypesCache.SingleOrDefault(dt => dt.Id == dishTypeId).Name;

                    await botClient.EditMessageTextAsync(
                        callbackQuery.Message.Chat.Id,
                        callbackQuery.Message.MessageId,
                        $"{(string.IsNullOrWhiteSpace(userOrderInfo) ? dishTypeName : $"<i>Твой заказ:{userOrderInfo}</i>\n{dishTypeName}")}:\n",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: inlineKeyboard);

                    _logger.LogInformation($"Пользователь [{senderName}] открыл [{dishTypeName}] на [{pageNumber}] странице");
                    return;
                }

                if (callbackQueryData.StartsWith(garnishMenuCallback))
                {
                    var dotsSegments = callbackQueryData.Split(":");
                    _ = int.TryParse(dotsSegments[1], out int parentDishTypeId);
                    _ = int.TryParse(dotsSegments[2], out int parentDishId);
                    _ = int.TryParse(dotsSegments[3], out int childDishTypeId);

                    var parentDish = dishesCache.SingleOrDefault(d => d.Id == parentDishId);
                    var children = dishesCache.Where(d => d.DishTypeId == childDishTypeId).OrderBy(d => d.Sort).ToList();

                    List<InlineKeyboardButton[]> rows = [];

                    foreach (var child in children)
                    {
                        rows.Add([InlineKeyboardButton.WithCallbackData(child.Name, $"{parentDish.GetClickIdentifier(child.Id)}/{pageNumber}")]);
                    }

                    rows.Add([InlineKeyboardButton.WithCallbackData("↩️ Назад", $"{dishesTypesCache.SingleOrDefault(dt => dt.Id == parentDishTypeId).GetClickIdentifier()}/{pageNumber}")]);

                    InlineKeyboardMarkup inlineKeyboard = new(rows);

                    string userOrderInfo = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(sender.Id)).OrderInfo;
                    string parentDishName = string.Empty;

                    if (childDishTypeId == 4)
                    {
                        parentDishName = $"Выбранное блюдо ({parentDish.Name.ToLowerInvariant()}) подаётся вместе с гарниром, он входит в стоимость {parentDish.Price} рублей. С каким гарниром нужно подать?";
                    }
                    if (childDishTypeId == 6)
                    {
                        parentDishName = $"К выбранному салату ({parentDish.Name.ToLowerInvariant()}) нужно выбрать заправку. Чем заправить?";
                    }

                    await botClient.EditMessageTextAsync(
                        callbackQuery.Message.Chat.Id,
                        callbackQuery.Message.MessageId,
                        $"{(string.IsNullOrWhiteSpace(userOrderInfo) ? parentDishName : $"<i>Твой заказ:{userOrderInfo}</i>\n{parentDishName}")}",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: inlineKeyboard);

                    if (childDishTypeId == 4)
                    {
                        _logger.LogInformation($"Пользователь [{senderName}] открыл меню выбора гарнира для блюда [{parentDish.Name}]");
                    }
                    if (childDishTypeId == 6)
                    {
                        _logger.LogInformation($"Пользователь [{senderName}] открыл меню выбора заправки для салата [{parentDish.Name}]");
                    }
                    return;
                }

                // кнопка "К списку блюд"
                if (callbackQueryData == backToMenuCallback)
                {
                    string userOrderInfo = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(sender.Id)).OrderInfo;
                    if (!string.IsNullOrWhiteSpace(userOrderInfo))
                    {
                        model.Text = $"{userOrderInfo}";
                    }
                    await botClient.HandleMenuPageClick(model);
                    if (!string.IsNullOrWhiteSpace(userOrderInfo))
                    {
                        _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"К списку блюд\"");
                    }
                    else
                    {
                        _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Вернуться к меню\"");
                    }
                    return;
                }

                // кнопка "Очистить корзину"
                if (callbackQueryData == clearCartCallback)
                {
                    await scope.ServiceProvider.GetRequiredService<UserService>().ClearCart(model.Sender);
                    await botClient.HandleCartClearClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Очистить корзину\"");
                    return;
                }

                // кнопка "Отправить заказ"
                if (callbackQueryData == makeOrderCallback)
                {
                    var orderText = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(model.Sender.Id)).OrderInfo;
                    await scope.ServiceProvider.GetRequiredService<UserService>().SendOrder(model.Sender);

                    await botClient.SendTextMessageAsync(currentAdmin.ExternalId, $"Новый заказ от {senderName}:\n{orderText}");

                    model.Text = await scope.ServiceProvider.GetRequiredService<AdminService>().GetPaymentInfo(orderText);
                    await botClient.HandleMakeOrderClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Отправить заказ\". Его заказ:\n{JsonConvert.SerializeObject(orderText)}");
                    return;
                }

                // кнопка "Перейти к оформлению"
                if (callbackQueryData == goToOrderCallback)
                {
                    model.Text = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(model.Sender.Id)).OrderInfo;
                    await botClient.HandleGoToOrderPageClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Перейти к оформлению\"");
                    return;
                }

                // подтверждение платежа
                if (callbackQueryData.StartsWith($"{dummyCallback}/confirm/"))
                {
                    var orderToConfirmId = long.Parse(callbackQueryData.Split($"{dummyCallback}/confirm/")[1]);

                    var orderToConfirm = db.Orders.SingleOrDefault(o => o.Id == orderToConfirmId);

                    var orderCreator = db.Clients.SingleOrDefault(c => c.Id == orderToConfirm.ClientId);

                    var orderToConfirmInfo = await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(orderCreator.ExternalId, true);
                    if (orderToConfirmInfo == null)
                    {
                        return;
                    }
                    var orderSumm = orderToConfirmInfo.TotalSumm;

                    await scope.ServiceProvider.GetRequiredService<AdminService>().ConfirmOrder(orderToConfirmId);
                    await botClient.SendTextMessageAsync(orderCreator.ExternalId, $"Заказ на сумму {orderSumm} руб. подтверждён администратором! 👍");

                    var unconfirmedOrders = await scope.ServiceProvider.GetRequiredService<AdminService>().GetUnconfirmedOrders();
                    model.Text = $"Неподтвержденные заказы на {CurrentDateTimeString}";
                    await botClient.HandleGetUnconfirmedOrders(model, unconfirmedOrders);
                    _logger.LogInformation($"Пользователь [{senderName}] запросил информацию о неподтвержденных заказах. Возвращаем:\n{JsonConvert.SerializeObject(unconfirmedOrders)}");
                    return;
                }

                if (callbackQueryData.StartsWith($"{dummyCallback}/changeValue/"))
                {
                    var settingToChangeId = long.Parse(callbackQueryData.Split($"{dummyCallback}/changeValue/")[1]);
                    var settingToChange = db.ClientSettings.SingleOrDefault(o => o.Id == settingToChangeId);
                    var creator = db.Clients.SingleOrDefault(c => c.Id == settingToChange.ClientId);
                    string oldValue = settingToChange.Value;
                    string newValue = oldValue.ToUpperInvariant() == "ДА" ? "Нет" : "Да";
                    settingToChange.Value = newValue;
                    db.SaveChanges();
                    var settingTypeToChange = db.SettingTypes.SingleOrDefault(o => o.Id == settingToChange.SettingId);
                    _logger.LogInformation($"Пользователь [{senderName}] изменил значение настройки {settingTypeToChange.Name} с {oldValue} на {newValue}");
                    model.Text = "Ниже перечислены настройки твоего профиля.\n\n<i>Чтобы изменить значение, нажми на строчку с именем настройки.\n✔️ — настройка включена,\n🚫 — настройка выключена</i>";
                    model.isEditOldMessage = true;
                    var settings = await scope.ServiceProvider.GetRequiredService<UserService>().GetUserSettings(dbUser.Id);
                    await botClient.HandleSettingsClick(model, settings);
                    return;
                }

                return;
            }

            // обычный текст, кнопки внизу экрана
            // update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            var message = update.Message;

            if (message == null)
            {
                return;
            }

            model.TelegramMessage = message;

            switch (message.Text)
            {
                case currentFoodButtonText:
                    string userOrderInfo = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(sender.Id)).OrderInfo;
                    if (!string.IsNullOrWhiteSpace(userOrderInfo))
                    {
                        model.Text = userOrderInfo;
                    }
                    model.isEditOldMessage = false;
                    await botClient.HandleMenuPageClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Меню\"");
                    return;
                case myOrderButtonText:
                    model.Text = (await scope.ServiceProvider.GetRequiredService<UserService>().GetUserOrderInfo(model.Sender.Id)).OrderInfo;
                    await botClient.HandleOrderPageClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] нажал на кнопку \"Мой заказ\"");
                    return;
                case settingsCommandText:
                    model.Text = "Ниже перечислены настройки твоего профиля.\n\n<i>Чтобы изменить значение, нажми на строчку с именем настройки.\n✔️ — настройка включена,\n🚫 — настройка выключена</i>";
                    model.isEditOldMessage = false;
                    var settings = await scope.ServiceProvider.GetRequiredService<UserService>().GetUserSettings(dbUser.Id);
                    await botClient.HandleSettingsClick(model, settings);
                    _logger.LogInformation($"Пользователь [{senderName}] открыл свои настройки");
                    break;
                case historyCommandText:
                    await botClient.HandleHistoryClick(model);
                    _logger.LogInformation($"Пользователь [{senderName}] открыл историю заказов");
                    break;
                case todayOrdersButtonText when sender.Id == currentAdmin.ExternalId:
                    var todayOrders = $"{await scope.ServiceProvider.GetRequiredService<AdminService>().GetAdminOrderInfo()}";
                    await botClient.SendTextMessageAsync(currentAdmin.ExternalId, todayOrders);
                    _logger.LogInformation($"Пользователь [{senderName}] запросил информацию о сегодняшних заказах. Возвращаем:\n{JsonConvert.SerializeObject(todayOrders)}");
                    return;
                case sendNotificationsButtonText when sender.Id == currentAdmin.ExternalId:
                    var users = await scope.ServiceProvider.GetRequiredService<AdminService>().GetUsersToNotify();
                    foreach (var user in users)
                    {
                        _logger.LogInformation($"Пользователю [{user.SystemName}] отправлено напоминание о заказе!");
                        await botClient.SendTextMessageAsync(user.ExternalId,
                        "Доброе утро, самое время сделать заказ!\n\n<i>Чтобы отключить напоминания, перейди в раздел настроек через меню (или команду /settings)</i>",
                        cancellationToken: model.CancellationToken,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                    }
                    return;
                case unconfirmedOrdersButtonText when sender.Id == currentAdmin.ExternalId:
                    var unconfirmedOrders = await scope.ServiceProvider.GetRequiredService<AdminService>().GetUnconfirmedOrders();
                    model.Text = $"Неподтвержденные заказы на {CurrentDateTimeString}";
                    model.isEditOldMessage = false;
                    await botClient.HandleGetUnconfirmedOrders(model, unconfirmedOrders);
                    _logger.LogInformation($"Пользователь [{senderName}] запросил информацию о неподтвержденных заказах. Возвращаем:\n{JsonConvert.SerializeObject(unconfirmedOrders)}");
                    return;

                default:
                    await botClient.HandleText(model, sender.Id == currentAdmin.ExternalId);
                    _logger.LogInformation($"Пользователь [{senderName}] написал текст: {message.Text}");
                    return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка в основном потоке!");
        }
        finally
        {
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery.Message.Chat.Id;

            await botClient.SetChatMenuButtonAsync(chatId: chatId, cancellationToken: cancellationToken);
        }
    }
}