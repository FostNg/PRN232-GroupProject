using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Entities;

public partial class RoomImage
{
    public Guid RoomImageId { get; set; }

    public Guid RoomId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;
}
