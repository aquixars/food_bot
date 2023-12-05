using System.Text;
using fobot.Database;
using fobot.Database.Models;
using fobot.POCOs;
using food_bot.POCOs;
using Microsoft.EntityFrameworkCore;
using static fobot.GlobalVariables;

namespace fobot.Services;

public class AdminService(IServiceProvider serviceProvider)
{
    private IServiceProvider _serviceProvider = serviceProvider;

    public async Task<string> GetAdminOrderInfo()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var getOrderTask = db.Orders.Where(o => o.IsSend == 1).ToListAsync();
        getOrderTask.Wait();
        var orders = getOrderTask.Result;

        var todayOrders = orders.Select(o => new
        {
            o.Id,
            isToday = new DateTime(o.CreatedInTicks).Date == DateTime.Today
        }).ToList().Where(el => el.isToday).ToList();

        if (todayOrders is null || todayOrders.Count == 0)
        {
            return "Сегодня никто ничего не заказал!";
        }

        var getOrderLinesTask = db.OrderLines
        .Join(db.Dishes, ol => ol.DishId, d => d.Id, (ol, d) => new { ol, d })
        .Where(ol => todayOrders.Select(el => el.Id).Contains(ol.ol.OrderId)).OrderBy(ol => ol.d.Sort).ToListAsync();

        getOrderLinesTask.Wait();
        var orderLines = getOrderLinesTask.Result;

        if (orderLines is null || orderLines.Count == 0)
        {
            return "Сегодня никто ничего не заказал!";
        }

        List<OrderViewElement> viewBeforeGroup = orderLines.Select(ol => new OrderViewElement
        {
            Ids = $"{ol.ol.DishId},{ol.ol.ChildDishId}",
            Amount = ol.ol.Amount
        }).ToList();

        List<OrderViewElement> view = [];

        foreach (var orderLine in viewBeforeGroup)
        {
            if (view.Select(v => v.Ids).Contains(orderLine.Ids))
            {
                view.SingleOrDefault(v => v.Ids == orderLine.Ids).Amount++;
            }
            else
            {
                view.Add(orderLine);
            }
        }

        StringBuilder formattedResult = new($"Заказы за {GlobalVariables.CurrentDateTimeString}");
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
            formattedResult.AppendLine($"{parentName}{(string.IsNullOrWhiteSpace(childName) ? string.Empty : $" + {childName.ToLowerInvariant()}")} x{item.Amount}");
            totalPrice += parent.Price * item.Amount;
        }

        formattedResult.AppendLine($"\nОбщая сумма заказов: {totalPrice} руб");


        string resultString = formattedResult.ToString();

        return resultString;
    }

    public async Task<List<Client>> GetActiveUsers()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var limitTicks = DateTime.Now.AddDays(-10).Ticks;

        var getActiveUsersIdsTask = db.Orders
            .Where(o => o.CreatedInTicks >= limitTicks)
            .Select(o => o.ClientId)
            .Distinct()
            .ToListAsync();
        getActiveUsersIdsTask.Wait();
        var activeUsersIds = getActiveUsersIdsTask.Result;

        var activeUsers = db.Clients
            .Where(c => activeUsersIds.Contains(c.Id))
            .ToList();

        return activeUsers;
    }

    public async Task<Client> GetActiveAdmin()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var admin = db.Admins.Where(a => a.IsActive == 1).FirstOrDefault();

        return db.Clients.FirstOrDefault(c => c.Id == admin.ClientId);
    }

    public async Task<string> GetPaymentInfo(string orderText)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var admin = db.Admins.Where(a => a.IsActive == 1).FirstOrDefault();

        return $@"<b>Заказ успешно оформлен!</b>
{orderText}
Сумму можно перевести на <b>{admin.Bank}</b> (<b>{admin.Initials}</b>), номер телефона:

<code>{admin.PhoneNumber}</code> (нажми, чтобы скопировать)

{admin.PhoneNumber} (активная кнопка для пользователей Android)";
    }

    public async Task<List<UnconfirmedOrderModel>> GetUnconfirmedOrders()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        var result = new List<UnconfirmedOrderModel>();

        var unconfirmedOrders = db.Orders.Where(o => o.IsConfirmed == 0 && o.IsSend == 1).ToList();
        var clientsWithUnconfirmedOrdersIds = unconfirmedOrders.Select(o => o.ClientId).Distinct().ToList();
        var clientsWithUnconfirmedOrders = db.Clients.Where(c => clientsWithUnconfirmedOrdersIds.Contains(c.Id)).ToList();

        foreach (var order in unconfirmedOrders)
        {
            var client = clientsWithUnconfirmedOrders.FirstOrDefault(c => c.Id == order.ClientId);
            var orderInfo = await userService.GetUserOrderInfo(client.ExternalId, true);
            if (orderInfo == null)
                continue;
            result.Add(new UnconfirmedOrderModel()
            {
                ClientId = client.Id,
                ClientName = !string.IsNullOrWhiteSpace(client.SystemName) ? client.SystemName : client.FirstName,
                OrderId = order.Id,
                OrderSumm = $"{orderInfo.TotalSumm} руб."
            });
        }

        return result;
    }

    public async Task ConfirmOrder(long orderId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalDBContext>();

        var order = db.Orders.SingleOrDefault(o => o.Id == orderId);

        order.IsConfirmed = 1;

        db.Orders.Update(order);

        await db.SaveChangesAsync();
    }
}