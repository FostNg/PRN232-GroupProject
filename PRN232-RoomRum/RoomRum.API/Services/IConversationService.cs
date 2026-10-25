using RoomRum.API.Common;
using RoomRum.API.DTOs.Chat;

namespace RoomRum.API.Services;

/// <summary>
/// One-to-one Tenant &lt;-&gt; Landlord chat scoped to a listing.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Returns the existing conversation for (tenantId, listingId) or creates
    /// it. Used by the chat screen and by the appointment flow, which opens a
    /// thread automatically when a viewing is booked.
    /// </summary>
    Task<ConversationDetailResponse> EnsureConversationAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailResponse> StartAsync(
        int tenantId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ConversationSummaryResponse>> GetMineAsync(
        int userId,
        string role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailResponse?> GetByIdAsync(
        int userId,
        int conversationId,
        int messagePageSize,
        CancellationToken cancellationToken = default);

    Task<MessageResponse?> SendMessageAsync(
        int userId,
        int conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<MarkReadResponse?> MarkAsReadAsync(
        int userId,
        int conversationId,
        CancellationToken cancellationToken = default);
}
