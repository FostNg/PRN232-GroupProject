using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class RoomRating
{
    public Guid RatingId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime RatingDate { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual RoomTenant RoomTenant { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
