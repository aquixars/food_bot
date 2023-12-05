namespace fobot.Database.Models;

public partial class Order
{
    public long Id { get; set; }

    public int ClientId { get; set; }

    public string Created { get; set; }

    public long CreatedInTicks { get; set; }

    public int IsSend { get; set; }

    public int IsConfirmed { get; set; }

    public virtual Client Client { get; set; }

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
}
