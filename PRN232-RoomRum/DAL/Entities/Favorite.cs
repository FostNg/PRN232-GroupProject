using System;
using System.Collections.Generic;

namespace DAL.Entities;

public partial class Favorite
{
    public Guid FavoriteId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
