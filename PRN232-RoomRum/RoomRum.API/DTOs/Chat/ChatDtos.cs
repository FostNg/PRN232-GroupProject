using System.ComponentModel.DataAnnotations;

namespace RoomRum.API.DTOs.Chat;

/// <summary>
/// Starts (or reuses) the conversation between a Tenant and the Landlord of a
/// listing. The database keeps at most one conversation per
/// (TenantId, ListingId), so a second call returns the existing thread.
/// </summary>
public class StartConversationRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ListingId { get; set; }

    /// <summary>Optional opening message; sent as the Tenant's first message.</summary>
    [StringLength(2000, MinimumLength = 1)]
    public string? FirstMessage { get; set; }
}

public class SendMessageRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>Conversation row for the chat inbox.</summary>
public sealed class ConversationSummaryResponse
{
    public int ConversationId { get; set; }

    public int ListingId { get; set; }

    public string ListingTitle { get; set; } = string.Empty;

    public string? ListingPrimaryImageUrl { get; set; }

    public string ListingStatus { get; set; } = string.Empty;

    /// <summary>Display name of the other participant, relative to the caller.</summary>
    public string OtherPartyName { get; set; } = string.Empty;

    public int OtherPartyId { get; set; }

    public bool OtherPartyIsLandlord { get; set; }

    public string? LastMessageContent { get; set; }

    public DateTime? LastMessageAt { get; set; }

    /// <summary>Unread messages addressed to the caller in this thread.</summary>
    public int UnreadCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Full conversation detail including its message history.</summary>
public sealed class ConversationDetailResponse
{
    public int ConversationId { get; set; }

    public int ListingId { get; set; }

    public string ListingTitle { get; set; } = string.Empty;

    public string? ListingPrimaryImageUrl { get; set; }

    public string ListingStatus { get; set; } = string.Empty;

    public int TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public int LandlordId { get; set; }

    public string LandlordName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// False once the listing is Deleted; the client hides the input box
    /// because the database rejects new messages at that point.
    /// </summary>
    public bool CanSendMessages { get; set; }

    public List<MessageResponse> Messages { get; set; } = [];
}

public sealed class MessageResponse
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    /// <summary>True when the caller is the sender, so the UI can align the bubble.</summary>
    public bool IsMine { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }
}

/// <summary>Result of marking a thread as read.</summary>
public sealed class MarkReadResponse
{
    public int ConversationId { get; set; }

    public int MarkedCount { get; set; }
}
