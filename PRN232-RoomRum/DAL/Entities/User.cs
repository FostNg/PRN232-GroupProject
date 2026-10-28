using System;
using System.Collections.Generic;

namespace RoomRum.DAL.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public string? Phone { get; set; }

    public string Name { get; set; } = null!;

    public string? Email { get; set; }

    public DateOnly? Dob { get; set; }

    public string? Gender { get; set; }

    public string? CitizenId { get; set; }

    public string Password { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public byte[]? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationExpiresAt { get; set; }

    public DateTime? EmailVerificationSentAt { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Message> MessageReceiverNavigations { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenderNavigations { get; set; } = new List<Message>();

    public virtual ICollection<Repairing> Repairings { get; set; } = new List<Repairing>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<RoomRating> RoomRatings { get; set; } = new List<RoomRating>();

    public virtual ICollection<RoomTenant> RoomTenants { get; set; } = new List<RoomTenant>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<Sharing> Sharings { get; set; } = new List<Sharing>();

    public virtual ICollection<UserRegistrationFile> UserRegistrationFiles { get; set; } = new List<UserRegistrationFile>();
}
