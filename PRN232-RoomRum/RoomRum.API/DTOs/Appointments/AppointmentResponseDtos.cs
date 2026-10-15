namespace RoomRum.API.DTOs.Appointments;

/// <summary>
/// A viewing appointment enriched with just enough listing and participant
/// context for the mobile list/detail screens to render without extra calls.
/// </summary>
public sealed class AppointmentResponse
{
    public int AppointmentId { get; set; }

    public int ListingId { get; set; }

    public string ListingTitle { get; set; } = string.Empty;

    public string ListingAddressLine { get; set; } = string.Empty;

    public string ListingDistrict { get; set; } = string.Empty;

    public string ListingCity { get; set; } = string.Empty;

    public string? ListingPrimaryImageUrl { get; set; }

    public string ListingStatus { get; set; } = string.Empty;

    public decimal ListingPrice { get; set; }

    public decimal? ListingLatitude { get; set; }

    public decimal? ListingLongitude { get; set; }

    public int TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public string? TenantPhone { get; set; }

    public int LandlordId { get; set; }

    public string LandlordName { get; set; } = string.Empty;

    public string? LandlordPhone { get; set; }

    /// <summary>Scheduled start, always serialised as UTC.</summary>
    public DateTime ScheduledAt { get; set; }

    public short DurationMinutes { get; set; }

    /// <summary>Computed by the database (ScheduledAt + DurationMinutes).</summary>
    public DateTime? ScheduledEndAt { get; set; }

    /// <summary>Deposit frozen at booking time, in VND.</summary>
    public decimal DepositAmountSnapshot { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Payment summary for this appointment, when a payment order exists.
    /// Drives the "Pay deposit" button on the client.
    /// </summary>
    public AppointmentPaymentSummary? Payment { get; set; }

    /// <summary>
    /// Statuses the calling user may move this appointment to right now.
    /// Computed server-side from the role and current status so the Flutter app
    /// does not duplicate the state machine.
    /// </summary>
    public List<string> AllowedNextStatuses { get; set; } = [];
}

public sealed class AppointmentPaymentSummary
{
    public int PaymentId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public string? TransactionCode { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// True when the client should show the payment action, i.e. the payment is
    /// Pending and the appointment is Confirmed.
    /// </summary>
    public bool CanPay { get; set; }

    public bool CanRetry { get; set; }

    public bool CanCancel { get; set; }
}

/// <summary>
/// Response returned when an appointment is created: the appointment itself
/// plus the chat conversation that was opened for it, so the client can jump
/// straight into messaging.
/// </summary>
public sealed class CreateAppointmentResponse
{
    public AppointmentResponse Appointment { get; set; } = null!;

    public int ConversationId { get; set; }
}
