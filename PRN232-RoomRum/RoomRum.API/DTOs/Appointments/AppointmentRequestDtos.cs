using System.ComponentModel.DataAnnotations;

namespace RoomRum.API.DTOs.Appointments;

/// <summary>
/// Tenant books a viewing slot for an Active listing.
/// The deposit snapshot is taken from the listing server-side, never trusted
/// from the client (dbo.TR_Appointments_Integrity, error 52150).
/// </summary>
public class CreateAppointmentRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ListingId { get; set; }

    /// <summary>
    /// Requested viewing time. Sent as an ISO-8601 timestamp with offset
    /// (for example 2026-10-10T14:00:00+07:00) so the server stores the correct
    /// UTC instant regardless of the device time zone.
    /// </summary>
    [Required]
    public DateTimeOffset ScheduledAt { get; set; }

    [Range(15, 180)]
    public short DurationMinutes { get; set; } = 30;

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>
/// Status transition requested by either the Tenant or the Landlord.
/// Allowed values depend on the caller's role and the current status; see
/// AppointmentStatusRules.
/// </summary>
public class UpdateAppointmentStatusRequest
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}
