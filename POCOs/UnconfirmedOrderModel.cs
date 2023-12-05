using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace food_bot.POCOs
{
    public class UnconfirmedOrderModel
    {
        public long ClientId { get; set; }
        public string ClientName { get; set; }
        public long OrderId { get; set; }
        public string OrderSumm { get; set; }
    }
}