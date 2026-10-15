using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Models;

public partial class Report
{
    public Guid ReportId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public string? Reason { get; set; }

    public DateTime? ReportDate { get; set; }

    public string? Status { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
