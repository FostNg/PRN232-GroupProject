using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Entities;

public partial class Appointment
{
    public Guid AppointmentId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoomId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public TimeOnly AppointmentTime { get; set; }

    public string Status { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
