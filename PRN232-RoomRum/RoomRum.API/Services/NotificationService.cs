using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Notifications;

namespace RoomRum.API.Services;

/// <summary>
/// Builds the notification feed from appointments, messages and payments.
/// No notification table exists in the v5.0 schema, so every item is derived
/// at read time. The feed is intentionally cheap: three bounded queries, then
/// an in-memory merge and sort.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 100;

    private readonly RoomRumDbContext _context;

    public NotificationService(RoomRumDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationFeedResponse> GetFeedAsync(
        int userId,
        string role,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = limit < 1 ? DefaultLimit : Math.Min(limit, MaxLimit);

        var isLandlord = role == UserRoles.Landlord;

        // ------------------------------------------------------------
        // 1. Appointments the caller participates in.
        // ------------------------------------------------------------
        var appointments = await
            (
                from appointment in _context.Appointments.AsNoTracking()
                join listing in _context.Listings.AsNoTracking()
                    on appointment.ListingId equals listing.ListingId
                join tenant in _context.Users.AsNoTracking()
                    on appointment.TenantId equals tenant.UserId
                join landlord in _context.Users.AsNoTracking()
                    on listing.LandlordId equals landlord.UserId
                where isLandlord
                    ? listing.LandlordId == userId
                    : appointment.TenantId == userId
                orderby appointment.CreatedAt descending
                select new
                {
                    appointment.AppointmentId,
                    appointment.ListingId,
                    appointment.Status,
                    appointment.ScheduledAt,
                    appointment.CreatedAt,
                    appointment.UpdatedAt,
                    ListingTitle = listing.Title,
                    TenantName = tenant.FullName,
                    LandlordName = landlord.FullName
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        // ------------------------------------------------------------
        // 2. Unread messages addressed to the caller.
        // ------------------------------------------------------------
        var unreadMessages = await
            (
                from message in _context.Messages.AsNoTracking()
                join conversation in _context.Conversations.AsNoTracking()
                    on message.ConversationId equals conversation.ConversationId
                join listing in _context.Listings.AsNoTracking()
                    on conversation.ListingId equals listing.ListingId
                join sender in _context.Users.AsNoTracking()
                    on message.SenderId equals sender.UserId
                where !message.IsRead
                    && (isLandlord
                        ? conversation.LandlordId == userId &&
                          message.SenderId != userId
                        : conversation.TenantId == userId &&
                          message.SenderId != userId)
                orderby message.SentAt descending
                select new
                {
                    message.MessageId,
                    message.ConversationId,
                    message.SenderId,
                    message.Content,
                    message.SentAt,
                    SenderName = sender.FullName,
                    conversation.ListingId,
                    ListingTitle = listing.Title
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        // ------------------------------------------------------------
        // 3. Payments awaiting action from this user.
        // ------------------------------------------------------------
        var payments = await
            (
                from payment in _context.Payments.AsNoTracking()
                join appointment in _context.Appointments.AsNoTracking()
                    on payment.AppointmentId equals appointment.AppointmentId
                join listing in _context.Listings.AsNoTracking()
                    on appointment.ListingId equals listing.ListingId
                where appointment.TenantId == userId
                    && payment.PaymentStatus == PaymentStatuses.Pending
                    && appointment.Status == AppointmentStatuses.Confirmed
                orderby payment.CreatedAt descending
                select new
                {
                    payment.PaymentId,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.PaymentStatus,
                    payment.CreatedAt,
                    appointment.AppointmentId,
                    appointment.ListingId,
                    ListingTitle = listing.Title
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        // ------------------------------------------------------------
        // Merge into one chronological feed.
        // ------------------------------------------------------------
        var items = new List<NotificationItemResponse>();

        foreach (var appointment in appointments)
        {
            var counterparty = isLandlord
                ? appointment.TenantName
                : appointment.LandlordName;

            items.Add(new NotificationItemResponse
            {
                Type = NotificationTypes.Appointment,
                Id = $"appointment:{appointment.AppointmentId}",
                Title = $"Viewing {appointment.Status.ToLowerInvariant()}",
                Body =
                    $"{counterparty} - {appointment.ListingTitle} at " +
                    $"{appointment.ScheduledAt.AsUtc():HH:mm dd/MM/yyyy} (UTC)",
                OccurredAt = (appointment.UpdatedAt ?? appointment.CreatedAt)
                    .AsUtc(),
                IsUnread = appointment.Status == AppointmentStatuses.Pending &&
                           isLandlord,
                Target = $"appointment:{appointment.AppointmentId}",
                AppointmentId = appointment.AppointmentId,
                ListingId = appointment.ListingId
            });
        }

        foreach (var message in unreadMessages)
        {
            var preview = message.Content.Length > 80
                ? message.Content[..80] + "..."
                : message.Content;

            items.Add(new NotificationItemResponse
            {
                Type = NotificationTypes.Message,
                Id = $"message:{message.MessageId}",
                Title = $"New message from {message.SenderName}",
                Body = $"{preview} - {message.ListingTitle}",
                OccurredAt = message.SentAt.AsUtc(),
                IsUnread = true,
                Target = $"conversation:{message.ConversationId}",
                ConversationId = message.ConversationId,
                ListingId = message.ListingId
            });
        }

        foreach (var payment in payments)
        {
            items.Add(new NotificationItemResponse
            {
                Type = NotificationTypes.Payment,
                Id = $"payment:{payment.PaymentId}",
                Title = "Booking deposit due",
                Body =
                    $"Pay {payment.Amount:N0} VND via {payment.PaymentMethod} " +
                    $"to reserve {payment.ListingTitle}",
                OccurredAt = payment.CreatedAt.AsUtc(),
                IsUnread = true,
                Target = $"payment:{payment.PaymentId}",
                PaymentId = payment.PaymentId,
                AppointmentId = payment.AppointmentId,
                ListingId = payment.ListingId
            });
        }

        var ordered = items
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        return new NotificationFeedResponse
        {
            Items = ordered,
            UnreadCount = ordered.Count(item => item.IsUnread)
        };
    }

    public async Task<NotificationUnreadCountResponse> GetUnreadCountAsync(
        int userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var isLandlord = role == UserRoles.Landlord;

        var unreadMessages = await
            (
                from message in _context.Messages.AsNoTracking()
                join conversation in _context.Conversations.AsNoTracking()
                    on message.ConversationId equals conversation.ConversationId
                where !message.IsRead
                    && message.SenderId != userId
                    && (isLandlord
                        ? conversation.LandlordId == userId
                        : conversation.TenantId == userId)
                select message.MessageId)
            .CountAsync(cancellationToken);

        var pendingAppointments = await _context.Appointments
            .AsNoTracking()
            .CountAsync(
                a =>
                    a.Status == AppointmentStatuses.Pending &&
                    (isLandlord
                        ? a.Listing!.LandlordId == userId
                        : a.TenantId == userId),
                cancellationToken);

        var pendingPayments = await _context.Payments
            .AsNoTracking()
            .CountAsync(
                p =>
                    p.PaymentStatus == PaymentStatuses.Pending &&
                    p.Appointment!.Status == AppointmentStatuses.Confirmed &&
                    p.Appointment!.TenantId == userId,
                cancellationToken);

        return new NotificationUnreadCountResponse
        {
            UnreadCount =
                unreadMessages + pendingAppointments + pendingPayments,
            UnreadMessages = unreadMessages,
            PendingAppointments = pendingAppointments,
            PendingPayments = pendingPayments
        };
    }
}
