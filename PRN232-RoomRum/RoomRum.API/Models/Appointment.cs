using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index(
    "ListingId",
    "ScheduledAt",
    "Status",
    Name = "IX_Appointments_Listing_Date")]

[Index(
    "TenantId",
    "ScheduledAt",
    "Status",
    Name = "IX_Appointments_Tenant_Date",
    IsDescending = new[] { false, true, false })]

public partial class Appointment
{
    [Key]
    public int AppointmentId { get; set; }

    public int ListingId { get; set; }

    public int TenantId { get; set; }

    [Precision(0)]
    public DateTime ScheduledAt { get; set; }

    public short DurationMinutes { get; set; }

    [Precision(0)]
    public DateTime? ScheduledEndAt { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal DepositAmountSnapshot { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    // ============================================================
    // LISTING
    // One Listing can have many Appointments.
    // ============================================================

    [ForeignKey("ListingId")]
    [InverseProperty("Appointments")]
    public virtual Listing Listing { get; set; } = null!;

    // ============================================================
    // PAYMENT
    // One Appointment can have at most one Payment.
    // ============================================================

    [InverseProperty("Appointment")]
    public virtual Payment? Payment { get; set; }

    // ============================================================
    // TENANT
    // One Tenant can have many Appointments.
    // ============================================================

    [ForeignKey("TenantId")]
    [InverseProperty("Appointments")]
    public virtual User Tenant { get; set; } = null!;
}