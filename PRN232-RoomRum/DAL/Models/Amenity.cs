using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class Amenity
{
    public Guid AmenityId { get; set; }

    public string AmenityName { get; set; } = null!;

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
