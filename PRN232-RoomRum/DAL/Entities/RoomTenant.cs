using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Entities;

public partial class RoomTenant
{
    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public DateTime HiredAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<RoomRating> RoomRatings { get; set; } = new List<RoomRating>();

    public virtual ICollection<RoomTenantHistory> History { get; set; } = new List<RoomTenantHistory>();

    public virtual User User { get; set; } = null!;
}
