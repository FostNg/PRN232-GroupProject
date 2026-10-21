using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Entities;

public partial class Repairing
{
    public Guid MaintenanceId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public string? Priority { get; set; }

    public string? RepairItems { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public DateOnly? ExpectingDate { get; set; }

    public string? Response { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
