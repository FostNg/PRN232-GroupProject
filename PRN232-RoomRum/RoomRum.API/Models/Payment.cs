using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index("PaymentStatus", "CreatedAt", Name = "IX_Payments_Status_CreatedAt", IsDescending = new[] { false, true })]
[Index("AppointmentId", Name = "UQ_Payments_Appointment", IsUnique = true)]
public partial class Payment
{
    [Key]
    public int PaymentId { get; set; }

    public int AppointmentId { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal Amount { get; set; }

    [StringLength(30)]
    public string PaymentPurpose { get; set; } = null!;

    [StringLength(30)]
    public string PaymentMethod { get; set; } = null!;

    [StringLength(20)]
    public string PaymentStatus { get; set; } = null!;

    [StringLength(100)]
    public string? TransactionCode { get; set; }

    [StringLength(500)]
    public string? FailureReason { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? PaidAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    [ForeignKey("AppointmentId")]
    [InverseProperty("Payment")]
    public virtual Appointment Appointment { get; set; } = null!;
}
