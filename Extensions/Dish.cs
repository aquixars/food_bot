using fobot.Database.Models;

namespace fobot.Extensions;

public static class DishExtensions
{
    public static string GetDishButtonText(this Dish dish)
    {
        return $"{dish.Name}{(dish.IsGarnishIncluded == 1? " (с гарниром)" : "")} — {dish.Price}р ";
    }

    public static string GetClickIdentifier(this Dish dish, int childId = 0)
    {
        return $"{dish.DishTypeId}:{dish.Id}{(childId == 0 ? string.Empty : $".{childId}")}";
    }
}