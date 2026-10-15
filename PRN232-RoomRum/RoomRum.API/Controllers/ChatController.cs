using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Chat;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

/// <summary>
/// One-to-one Tenant &lt;-&gt; Landlord chat, scoped to a listing.
///
/// The database guarantees at most one conversation per (TenantId, ListingId),
/// so POST /api/Chat/conversations is idempotent: calling it again returns the
/// existing thread instead of creating a duplicate.
/// </summary>
[Route("api/[controller]")]
[Authorize]
public sealed class ChatController : ApiControllerBase
{
    private readonly IConversationService _conversationService;

    public ChatController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    // ============================================================
    // START / REUSE A CONVERSATION
    // POST: /api/Chat/conversations
    // ============================================================

    [HttpPost("conversations")]
    [Authorize(Roles = UserRoles.Tenant)]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationDetailResponse>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartConversation(
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        var result = await _conversationService.StartAsync(
            tenantId,
            request,
            cancellationToken);

        return CreatedEnvelope(
            result,
            "Conversation ready.");
    }

    // ============================================================
    // INBOX
    // GET: /api/Chat/conversations
    // ============================================================

    [HttpGet("conversations")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<ConversationSummaryResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        var result = await _conversationService.GetMineAsync(
            userId,
            GetRole(),
            page,
            pageSize,
            cancellationToken);

        return OkEnvelope(result);
    }

    // ============================================================
    // CONVERSATION DETAIL + MESSAGE HISTORY
    // GET: /api/Chat/conversations/{conversationId}
    // ============================================================

    [HttpGet("conversations/{conversationId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationDetailResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(
        int conversationId,
        [FromQuery] int messagePageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        if (conversationId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid conversation ID."));
        }

        var result = await _conversationService.GetByIdAsync(
            userId,
            conversationId,
            messagePageSize,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Conversation not found."));
        }

        return OkEnvelope(result);
    }

    // ============================================================
    // SEND A MESSAGE
    // POST: /api/Chat/conversations/{conversationId}/messages
    // ============================================================

    [HttpPost("conversations/{conversationId:int}/messages")]
    [ProducesResponseType(
        typeof(ApiResponse<MessageResponse>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendMessage(
        int conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        if (conversationId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid conversation ID."));
        }

        var result = await _conversationService.SendMessageAsync(
            userId,
            conversationId,
            request,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Conversation not found."));
        }

        return CreatedEnvelope(result, "Message sent.");
    }

    // ============================================================
    // MARK CONVERSATION AS READ
    // PATCH: /api/Chat/conversations/{conversationId}/read
    // ============================================================

    [HttpPatch("conversations/{conversationId:int}/read")]
    [ProducesResponseType(
        typeof(ApiResponse<MarkReadResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        int conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        if (conversationId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid conversation ID."));
        }

        var result = await _conversationService.MarkAsReadAsync(
            userId,
            conversationId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Conversation not found."));
        }

        return OkEnvelope(
            result,
            $"{result.MarkedCount} message(s) marked as read.");
    }
}
