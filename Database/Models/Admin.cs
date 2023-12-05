using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class Admin
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public string Bank { get; set; }

    public string PhoneNumber { get; set; }

    public string Initials { get; set; }

    public int IsActive { get; set; }

    public virtual Client Client { get; set; }
}
