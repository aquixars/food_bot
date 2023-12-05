using System.Text;
using fobot.Database;
using fobot.Database.Models;
using fobot.POCOs;
using food_bot.POCOs;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using static fobot.GlobalVariables;

namespace fobot.Services;

public class UserService(IServiceProvider serviceProvider)
{
    private IServiceProvider _serviceProvider = serviceProvider;

    public async Task SaveUserActivityInfo(User user)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var dbUser = db.Clients.FirstOrDefault(u => u.ExternalId == user.Id);
        if (dbUser == null)
        {
            dbUser = new Client
            {
                ExternalId = user.Id,
                UserName = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                LastMessageCreated = CurrentDateTimeString
            };
            await db.Clients.AddAsync(dbUser);
        }
        else
        {
            dbUser.LastMessageCreated = CurrentDateTimeString;
            dbUser.UserName = user.Username;
            dbUser.FirstName = user.FirstName;
            dbUser.LastName = user.LastName;
            db.Clients.Update(dbUser);
        }

        await db.SaveChangesAsync();
    }

    public async Task<UserOrderInfo> GetUserOrderInfo(long externalId, bool isSearchConfirmation = false)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var result = new UserOrderInfo();

        var getClientTask = db.Clients.SingleOrDefaultAsync(c => c.ExternalId == externalId);
        getClientTask.Wait();
        var client = getClientTask.Result;

        var getOrderTaskQuery = db.Orders.Where(o => o.ClientId == client.Id);
        if (isSearchConfirmation)
        {
            getOrderTaskQuery = getOrderTaskQuery.Where(o => o.IsConfirmed == 0 && o.IsSend == 1);
        }
        else
        {
            getOrderTaskQuery = getOrderTaskQuery.Where(o => o.IsSend == 0);
        }
        var getOrderTask = getOrderTaskQuery.SingleOrDefaultAsync();
        getOrderTask.Wait();
        var order = getOrderTask.Result;

        if (order is null)
        {
            return result;
        }

        var getOrderLinesTask = db.OrderLines.Where(ol => ol.OrderId == order.Id).ToListAsync();
        getOrderLinesTask.Wait();
        var orderLines = getOrderLinesTask.Result;

        if (orderLines is null || orderLines.Count == 0)
        {
            return result;
        }

        List<OrderViewElement> view = orderLines.Select(ol => new OrderViewElement
        {
            Ids = $"{ol.DishId},{ol.ChildDishId}",
            Amount = ol.Amount
        }).ToList();

        StringBuilder formattedResult = new();
        formattedResult.AppendLine();
        int totalPrice = 0;

        foreach (var item in view)
        {
            var dishIdsArray = item.Ids.Split(",");
            _ = int.TryParse(dishIdsArray[0], out int parentId);
            _ = int.TryParse(dishIdsArray[1], out int childId);
            var parent = dishesCache.SingleOrDefault(d => d.Id == parentId);
            var parentName = parent?.Name;
            var childName = dishesCache.SingleOrDefault(d => d.Id == childId)?.Name;
            formattedResult.AppendLine($"— {parentName}{(string.IsNullOrWhiteSpace(childName) ? string.Empty : $" + {childName.ToLowerInvariant()}")} x{item.Amount} ({parent.Price * item.Amount} руб.)");
            totalPrice += parent.Price * item.Amount;
        }

        formattedResult.AppendLine($"Общая сумма заказа: {totalPrice} руб.");

        string resultString = formattedResult.ToString();

        result.OrderInfo = resultString;
        result.TotalSumm = totalPrice;

        return result;
    }

    public async Task ClearCart(User user)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var getClientTask = db.Clients.SingleOrDefaultAsync(c => c.ExternalId == user.Id);
        getClientTask.Wait();
        var client = getClientTask.Result;

        var getOrderTask = db.Orders.SingleOrDefaultAsync(o => o.ClientId == client.Id && o.IsSend == 0);
        getOrderTask.Wait();
        var order = getOrderTask.Result;

        if (order is null)
        {
            return;
        }

        var getOrderLinesTask = db.OrderLines.Where(ol => ol.OrderId == order.Id).ToListAsync();
        getOrderLinesTask.Wait();
        var orderLines = getOrderLinesTask.Result;

        db.OrderLines.RemoveRange(orderLines);
        db.Orders.Remove(order);

        await db.SaveChangesAsync();
    }

    public async Task SendOrder(User user)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var getClientTask = db.Clients.SingleOrDefaultAsync(c => c.ExternalId == user.Id);
        getClientTask.Wait();
        var client = getClientTask.Result;

        var getOrderTask = db.Orders.Where(o => o.ClientId == client.Id && o.IsSend == 0).ToListAsync();
        getOrderTask.Wait();
        var orders = getOrderTask.Result;

        if (orders is null || orders.Count == 0)
        {
            return;
        }

        foreach (var order in orders)
        {
            order.IsSend = 1;
            order.Created = CurrentDateTimeString;
            order.CreatedInTicks = DateTime.Now.Ticks;
        }

        db.Orders.UpdateRange(orders);

        await db.SaveChangesAsync();
    }
}