using System;
using System.Collections.Generic;

namespace DAL.Entities;

public partial class Room
{
    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public string? Status { get; set; }

    public string? Type { get; set; }

    public string? Title { get; set; }

    public string? Address { get; set; }

    public decimal? Area { get; set; }

    public decimal? ElectricityFee { get; set; }

    public decimal? WaterFee { get; set; }

    public decimal? ServiceFee { get; set; }

    public decimal? Deposit { get; set; }

    public decimal? Price { get; set; }

    public string? Description { get; set; }

    public string? Rule { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Repairing> Repairings { get; set; } = new List<Repairing>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual ICollection<RoomImage> RoomImages { get; set; } = new List<RoomImage>();

    public virtual ICollection<RoomRating> RoomRatings { get; set; } = new List<RoomRating>();

    public virtual ICollection<RoomTenant> RoomTenants { get; set; } = new List<RoomTenant>();

    public virtual ICollection<Sharing> Sharings { get; set; } = new List<Sharing>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
}
