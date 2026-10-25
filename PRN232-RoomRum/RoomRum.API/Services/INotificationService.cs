using RoomRum.API.DTOs.Notifications;

namespace RoomRum.API.Services;

/// <summary>
/// In-app notification feed.
///
/// The v5.0 schema deliberately excludes push-notification tables, so the feed
/// is derived from the business data that already exists: appointments,
/// messages and payments. This keeps the schema frozen while still giving the
/// mobile app a real notification screen.
/// </summary>
public interface INotificationService
{
    Task<NotificationFeedResponse> GetFeedAsync(
        int userId,
        string role,
        int limit,
        CancellationToken cancellationToken = default);

    Task<NotificationUnreadCountResponse> GetUnreadCountAsync(
        int userId,
        string role,
        CancellationToken cancellationToken = default);
}
