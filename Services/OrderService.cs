using fobot.Database;
using fobot.Database.Models;
using fobot.Extensions;
using Microsoft.EntityFrameworkCore;
using static fobot.GlobalVariables;
using fobot.POCOs;

namespace fobot.Services;

public class OrderService(IServiceProvider serviceProvider)
{
    private IServiceProvider _serviceProvider = serviceProvider;

    public async Task InitMenuCallbacks()
    {
        foreach (var dish in dishesCache)
        {
            FoodClickCallbackModel foodCallback = new()
            {
                IsGarnishIncluded = dish.IsGarnishIncluded == 1,
                IsFlavoringIncluded = dish.IsFlavoringIncluded == 1,
                CallbackFunctionName = dish.GetClickIdentifier(),
                CallbackFunction = async (userId, childId) => await AddOrderLine(userId, dish.Id, childId)
            };
            foodCallbacks.Add(foodCallback);
        }
    }

    private async Task AddOrderLine(int userId, int dishId, int childId = 0)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        Order order = db.Orders.SingleOrDefault(o => o.ClientId == userId && o.IsSend == 0);

        if (order is null)
        {
            order = new Order
            {
                ClientId = userId,
                Created = CurrentDateTimeString,
                CreatedInTicks = DateTime.Now.Ticks
            };

            await db.Orders.AddAsync(order);
        }
        else
        {
            order.Created = CurrentDateTimeString;
            order.CreatedInTicks = DateTime.Now.Ticks;

            db.Orders.Update(order);
        }
        await db.SaveChangesAsync();

        var task = db.OrderLines.SingleOrDefaultAsync(ol => ol.OrderId == order.Id && ol.DishId == dishId && ol.ChildDishId == (childId == 0 ? null : childId));
        task.Wait();
        var orderLine = task.Result;

        if (orderLine is null)
        {
            orderLine = new()
            {
                OrderId = order.Id,
                DishId = dishId,
                ChildDishId = childId == 0 ? null : childId,
                Amount = 1
            };

            await db.OrderLines.AddAsync(orderLine);
        }
        else
        {
            orderLine.Amount++;
            db.OrderLines.Update(orderLine);
        }

        await db.SaveChangesAsync();
    }
}