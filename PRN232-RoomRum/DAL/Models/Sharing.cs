using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class Sharing
{
    public Guid SharingId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public string? Title { get; set; }

    public int? Quantity { get; set; }

    public string? Gender { get; set; }

    public decimal? SharingFee { get; set; }

    public string? Introduction { get; set; }

    public string? Rule { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
