using fobot.Database.Models;
using fobot.POCOs;

namespace fobot;

public static class GlobalVariables
{
    public const string currentFoodButtonText = "📋Меню";
    public const string myOrderButtonText = "🛒Мой заказ";
    public const string todayOrdersButtonText = "✍️Сегодняшний список";
    public const string unconfirmedOrdersButtonText = "👍Ожидающие заказы";

    public const string previousPageButtonText = "⬅️ Назад";
    public const string nextPageButtonText = "Далее ➡️";

    public const string backToMenuCallback = "back.to.menu";
    public const string garnishMenuCallback = "garnish.menu";
    public const string clearCartCallback = "cart.clear";
    public const string makeOrderCallback = "make.order";
    public const string goToOrderCallback = "open.my.order";
    public static string dummyCallback = Guid.NewGuid().ToString();
    public static string CurrentDateTimeString => $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}";

    public static string LogsPath = $"{AppContext.BaseDirectory}{Path.DirectorySeparatorChar}logs";

    public const int pageSize = 5;

    public static List<Dish> dishesCache = [];
    public static List<DishType> dishesTypesCache = [];
    public static List<FoodClickCallbackModel> foodCallbacks = [];
}