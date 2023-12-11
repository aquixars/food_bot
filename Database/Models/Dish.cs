using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class Dish
{
    public int Id { get; set; }

    public int? DishTypeId { get; set; }

    public string Name { get; set; }

    public int Price { get; set; }

    public int IsGarnishIncluded { get; set; }

    public int IsFlavoringIncluded { get; set; }

    public int? Sort { get; set; }

    public virtual DishType DishType { get; set; }

    public virtual ICollection<OrderLine> OrderLineChildDishes { get; set; } = new List<OrderLine>();

    public virtual ICollection<OrderLine> OrderLineDishes { get; set; } = new List<OrderLine>();
}
