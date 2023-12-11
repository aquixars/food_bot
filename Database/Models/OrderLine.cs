using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class OrderLine
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int DishId { get; set; }

    public int? ChildDishId { get; set; }

    public int Amount { get; set; }

    public virtual Dish ChildDish { get; set; }

    public virtual Dish Dish { get; set; }

    public virtual Order Order { get; set; }
}
