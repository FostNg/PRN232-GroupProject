namespace RoomRum.API.DTOs.Notifications;

/// <summary>Category of a feed item, used by the client to pick an icon.</summary>
public static class NotificationTypes
{
    public const string Appointment = "Appointment";
    public const string Message = "Message";
    public const string Payment = "Payment";
}

public sealed class NotificationItemResponse
{
    public string Type { get; set; } = string.Empty;

    /// <summary>Stable id within its type, for list keys.</summary>
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    public bool IsUnread { get; set; }

    /// <summary>
    /// Deep-link target for the client, e.g.
    /// "appointment:12", "conversation:5", "payment:9".
    /// </summary>
    public string Target { get; set; } = string.Empty;

    public int? AppointmentId { get; set; }

    public int? ConversationId { get; set; }

    public int? ListingId { get; set; }

    public int? PaymentId { get; set; }
}

public sealed class NotificationFeedResponse
{
    public List<NotificationItemResponse> Items { get; set; } = [];

    public int UnreadCount { get; set; }

    /// <summary>
    /// True when the feed is a projection of business data rather than a
    /// dedicated notification table; documented so the client can label it.
    /// </summary>
    public bool IsDerivedFeed { get; set; } = true;
}

public sealed class NotificationUnreadCountResponse
{
    public int UnreadCount { get; set; }

    public int UnreadMessages { get; set; }

    public int PendingAppointments { get; set; }

    public int PendingPayments { get; set; }
}
