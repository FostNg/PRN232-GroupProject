using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Notifications;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

/// <summary>
/// In-app notification feed.
///
/// The v5.0 database intentionally has no push-notification tables, so this
/// feed is projected from appointments, unread messages and payable deposits.
/// The client polls it (or refreshes on resume) and shows a local notification
/// when new items appear.
/// </summary>
[Route("api/[controller]")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // ============================================================
    // FEED
    // GET: /api/Notifications
    // ============================================================

    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<NotificationFeedResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        var result = await _notificationService.GetFeedAsync(
            userId,
            GetRole(),
            limit,
            cancellationToken);

        return OkEnvelope(result);
    }

    // ============================================================
    // BADGE COUNT
    // GET: /api/Notifications/unread-count
    // ============================================================

    [HttpGet("unread-count")]
    [ProducesResponseType(
        typeof(ApiResponse<NotificationUnreadCountResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        var result = await _notificationService.GetUnreadCountAsync(
            userId,
            GetRole(),
            cancellationToken);

        return OkEnvelope(result);
    }
}
