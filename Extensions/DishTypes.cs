using fobot.Database.Models;

namespace fobot.Extensions;

public static class DishTypesExtensions
{
    public static string GetDishTypeButtonText(this DishType dishType)
    {
        return dishType.Name;
    }

    public static string GetClickIdentifier(this DishType dishType)
    {
        return $"dishType:{dishType.Id}";
    }
}