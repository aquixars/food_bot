using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class DishType
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Dish> Dishes { get; set; } = new List<Dish>();
}
