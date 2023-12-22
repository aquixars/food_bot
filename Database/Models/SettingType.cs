using System;
using System.Collections.Generic;

namespace fobot.Database.Models;

public partial class SettingType
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<ClientSetting> ClientSettings { get; set; } = new List<ClientSetting>();
}
