using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class Client
{
    public int Id { get; set; }

    public long ExternalId { get; set; }

    public string UserName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string SystemName { get; set; }

    public string LastMessageCreated { get; set; }

    public virtual ICollection<Admin> Admins { get; set; } = new List<Admin>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
