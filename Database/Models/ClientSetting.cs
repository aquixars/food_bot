using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class ClientSetting
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public int SettingId { get; set; }

    public string Value { get; set; }

    public virtual Client Client { get; set; }

    public virtual SettingType Setting { get; set; }
}
