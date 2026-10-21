using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Chat;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

/// <summary>
/// Tenant &lt;-&gt; Landlord chat.
///
/// Database invariants honoured here (dbo.TR_Conversations_Integrity and
/// dbo.TR_Messages_Integrity):
///   - participants and listing are immutable;
///   - the landlord must still own the listing;
///   - a new conversation requires an Active or Reserved listing;
///   - a new message requires an active participant and a non-deleted listing;
///   - messages can never be deleted, and only IsRead may change.
/// </summary>
public sealed class ConversationService : IConversationService
{
    private const int DefaultMessagePageSize = 50;
    private const int MaxMessagePageSize = 200;

    private readonly RoomRumDbContext _context;

    public ConversationService(RoomRumDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // ENSURE / START
    // ============================================================

    public async Task<ConversationDetailResponse> StartAsync(
        int tenantId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await EnsureConversationEntityAsync(
            tenantId,
            request.ListingId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.FirstMessage))
        {
            var content = request.FirstMessage.Trim();

            var alreadyHasMessages = await _context.Messages
                .AsNoTracking()
                .AnyAsync(
                    m => m.ConversationId == conversation.ConversationId,
                    cancellationToken);

            // Only send the opening line when the thread is still empty, so a
            // retried call does not duplicate the message.
            if (!alreadyHasMessages)
            {
                await InsertMessageAsync(
                    conversation,
                    tenantId,
                    content,
                    cancellationToken);
            }
        }

        return await LoadDetailAsync(
            conversation.ConversationId,
            tenantId,
            DefaultMessagePageSize,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The conversation was created but could not be loaded.");
    }

    public async Task<ConversationDetailResponse> EnsureConversationAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await EnsureConversationEntityAsync(
            tenantId,
            listingId,
            cancellationToken);

        return await LoadDetailAsync(
            conversation.ConversationId,
            tenantId,
            DefaultMessagePageSize,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The conversation could not be loaded.");
    }

    private async Task<Conversation> EnsureConversationEntityAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken)
    {
        var tenant = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.UserId == tenantId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "The authenticated user no longer exists.");

        if (tenant.Role != UserRoles.Tenant || !tenant.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Only an active Tenant account can start a conversation.");
        }

        var listing = await _context.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.ListingId == listingId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Listing does not exist.");

        if (listing.Status == ListingStatuses.Deleted)
        {
            throw new InvalidOperationException(
                "A conversation cannot be started for a Deleted listing.");
        }

        if (listing.LandlordId == tenantId)
        {
            throw new ArgumentException(
                "You cannot start a conversation with yourself.");
        }

        var existing = await _context.Conversations
            .FirstOrDefaultAsync(
                c =>
                    c.TenantId == tenantId &&
                    c.ListingId == listingId,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        // Mirrors TR_Conversations_Integrity (error 52170) for new threads, so
        // the caller is told the listing is not open for chat instead of a
        // vague "may have been deleted" message from the catch-all below. An
        // existing thread is returned above and keeps working even if the
        // listing is no longer Active/Reserved, as long as it is not Deleted.
        if (listing.Status != ListingStatuses.Active &&
            listing.Status != ListingStatuses.Reserved)
        {
            throw new InvalidOperationException(
                $"A conversation cannot be started for a listing in status " +
                $"'{listing.Status}'. Only Active or Reserved listings accept " +
                "new chat requests.");
        }

        var conversation = new Conversation
        {
            TenantId = tenantId,
            LandlordId = listing.LandlordId,
            ListingId = listingId,

            // Set explicitly instead of relying on the DEFAULT SYSUTCDATETIME()
            // rounding, otherwise the first message (SentAt truncated to a whole
            // second) can land before CreatedAt and trip
            // CK_Conversations_LastMessage (error 547).
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        _context.Conversations.Add(conversation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateKey(exception))
        {
            // Another request created the same thread first; the unique index
            // UX_Conversations_Tenant_Listing guarantees only one exists.
            _context.Entry(conversation).State = EntityState.Detached;

            return await _context.Conversations
                .AsNoTracking()
                .FirstAsync(
                    c =>
                        c.TenantId == tenantId &&
                        c.ListingId == listingId,
                    cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The conversation could not be created. The listing may have " +
                "been deleted or is not available for chat.",
                exception);
        }

        return conversation;
    }

    // ============================================================
    // INBOX
    // ============================================================

    public async Task<PagedResult<ConversationSummaryResponse>> GetMineAsync(
        int userId,
        string role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var query = _context.Conversations
            .AsNoTracking()
            .Where(c => role == UserRoles.Landlord
                ? c.LandlordId == userId
                : c.TenantId == userId);

        var totalItems = await query.CountAsync(cancellationToken);

        var conversationIds = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ThenByDescending(c => c.ConversationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.ConversationId)
            .ToListAsync(cancellationToken);

        if (conversationIds.Count == 0)
        {
            return PagedResult<ConversationSummaryResponse>.Create(
                [],
                page,
                pageSize,
                totalItems);
        }

        var rows = await QuerySummaryProjection()
            .Where(row => conversationIds.Contains(row.ConversationId))
            .ToListAsync(cancellationToken);

        var items = conversationIds
            .Select(id => rows.FirstOrDefault(r => r.ConversationId == id))
            .Where(row => row is not null)
            .Select(row => MapSummary(row!, userId))
            .ToList();

        return PagedResult<ConversationSummaryResponse>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }

    // ============================================================
    // DETAIL
    // ============================================================

    public async Task<ConversationDetailResponse?> GetByIdAsync(
        int userId,
        int conversationId,
        int messagePageSize,
        CancellationToken cancellationToken = default)
    {
        if (messagePageSize < 1)
        {
            messagePageSize = DefaultMessagePageSize;
        }

        messagePageSize = Math.Min(messagePageSize, MaxMessagePageSize);

        return await LoadDetailAsync(
            conversationId,
            userId,
            messagePageSize,
            cancellationToken);
    }

    private async Task<ConversationDetailResponse?> LoadDetailAsync(
        int conversationId,
        int viewerId,
        int messagePageSize,
        CancellationToken cancellationToken)
    {
        var header = await QueryHeaderProjection()
            .FirstOrDefaultAsync(
                h => h.ConversationId == conversationId,
                cancellationToken);

        if (header is null)
        {
            return null;
        }

        if (header.TenantId != viewerId && header.LandlordId != viewerId)
        {
            throw new UnauthorizedAccessException(
                "You are not a participant of this conversation.");
        }

        // Newest messages last, limited to the most recent page, then reversed
        // into chronological order for the chat UI.
        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.MessageId)
            .Take(messagePageSize)
            .Select(m => new MessageResponse
            {
                MessageId = m.MessageId,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender!.FullName,
                Content = m.Content,
                SentAt = m.SentAt,
                IsRead = m.IsRead
            })
            .ToListAsync(cancellationToken);

        messages.Reverse();

        foreach (var message in messages)
        {
            message.SentAt = message.SentAt.AsUtc();
            message.IsMine = message.SenderId == viewerId;
        }

        return new ConversationDetailResponse
        {
            ConversationId = header.ConversationId,
            ListingId = header.ListingId,
            ListingTitle = header.ListingTitle,
            ListingPrimaryImageUrl = header.ListingPrimaryImageUrl,
            ListingStatus = header.ListingStatus,
            TenantId = header.TenantId,
            TenantName = header.TenantName,
            LandlordId = header.LandlordId,
            LandlordName = header.LandlordName,
            CreatedAt = header.CreatedAt.AsUtc(),
            LastMessageAt = header.LastMessageAt.AsUtc(),

            // Mirrors the trigger rule: no new messages once the listing is
            // deleted, and both participants must still be active.
            CanSendMessages =
                header.ListingStatus != ListingStatuses.Deleted &&
                header.TenantIsActive &&
                header.LandlordIsActive,

            Messages = messages
        };
    }

    // ============================================================
    // SEND MESSAGE
    // ============================================================

    public async Task<MessageResponse?> SendMessageAsync(
        int userId,
        int conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Message content is required.");
        }

        var content = request.Content.Trim();

        if (content.Length > 2000)
        {
            throw new ArgumentException(
                "Message content must not exceed 2000 characters.");
        }

        var header = await QueryHeaderProjection()
            .FirstOrDefaultAsync(
                h => h.ConversationId == conversationId,
                cancellationToken);

        if (header is null)
        {
            return null;
        }

        if (header.TenantId != userId && header.LandlordId != userId)
        {
            throw new UnauthorizedAccessException(
                "You are not a participant of this conversation.");
        }

        if (header.ListingStatus == ListingStatuses.Deleted)
        {
            throw new InvalidOperationException(
                "This listing was deleted, so the conversation is closed.");
        }

        var senderIsActive = header.TenantId == userId
            ? header.TenantIsActive
            : header.LandlordIsActive;

        if (!senderIsActive)
        {
            throw new UnauthorizedAccessException(
                "Your account is deactivated and cannot send messages.");
        }

        var conversation = new Conversation
        {
            ConversationId = conversationId
        };

        var message = await InsertMessageAsync(
            conversation,
            userId,
            content,
            cancellationToken);

        return new MessageResponse
        {
            MessageId = message.MessageId,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = header.TenantId == userId
                ? header.TenantName
                : header.LandlordName,
            IsMine = true,
            Content = message.Content,
            SentAt = message.SentAt.AsUtc(),
            IsRead = message.IsRead
        };
    }

    private async Task<Message> InsertMessageAsync(
        Conversation conversation,
        int senderId,
        string content,
        CancellationToken cancellationToken)
    {
        var message = new Message
        {
            ConversationId = conversation.ConversationId,
            SenderId = senderId,
            Content = content,

            // Whole seconds: DATETIME2(0) would otherwise round up and the
            // stored SentAt could land one second in the future, which
            // TR_Messages_Integrity rejects with error 52181.
            SentAt = DateTimeExtensions.UtcNowToSecond(),

            IsRead = false
        };

        _context.Messages.Add(message);

        try
        {
            // dbo.TR_Messages_UpdateConversationLastMessage maintains
            // Conversations.LastMessageAt after the insert.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The message could not be sent. The listing may have been " +
                "deleted or a participant account was deactivated.",
                exception);
        }

        return message;
    }

    // ============================================================
    // MARK AS READ
    // ============================================================

    public async Task<MarkReadResponse?> MarkAsReadAsync(
        int userId,
        int conversationId,
        CancellationToken cancellationToken = default)
    {
        var header = await QueryHeaderProjection()
            .FirstOrDefaultAsync(
                h => h.ConversationId == conversationId,
                cancellationToken);

        if (header is null)
        {
            return null;
        }

        if (header.TenantId != userId && header.LandlordId != userId)
        {
            throw new UnauthorizedAccessException(
                "You are not a participant of this conversation.");
        }

        // IsRead is the only message column the database allows to change.
        var markedCount = await _context.Messages
            .Where(m =>
                m.ConversationId == conversationId &&
                m.SenderId != userId &&
                !m.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.IsRead, true),
                cancellationToken);

        return new MarkReadResponse
        {
            ConversationId = conversationId,
            MarkedCount = markedCount
        };
    }

    // ============================================================
    // PROJECTIONS
    // ============================================================

    private IQueryable<ConversationHeaderRow> QueryHeaderProjection()
    {
        return
            from conversation in _context.Conversations.AsNoTracking()
            join listing in _context.Listings.AsNoTracking()
                on conversation.ListingId equals listing.ListingId
            join tenant in _context.Users.AsNoTracking()
                on conversation.TenantId equals tenant.UserId
            join landlord in _context.Users.AsNoTracking()
                on conversation.LandlordId equals landlord.UserId
            select new ConversationHeaderRow
            {
                ConversationId = conversation.ConversationId,
                ListingId = listing.ListingId,
                ListingTitle = listing.Title,
                ListingStatus = listing.Status,
                ListingPrimaryImageUrl = _context.ListingImages
                    .AsNoTracking()
                    .Where(image =>
                        image.ListingId == listing.ListingId &&
                        image.IsPrimary)
                    .Select(image => "/api/Listings/" + image.ListingId + "/images/" + image.ImageId + "/content")
                    .FirstOrDefault(),
                TenantId = tenant.UserId,
                TenantName = tenant.FullName,
                TenantIsActive = tenant.IsActive,
                LandlordId = landlord.UserId,
                LandlordName = landlord.FullName,
                LandlordIsActive = landlord.IsActive,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt
            };
    }

    private IQueryable<ConversationSummaryRow> QuerySummaryProjection()
    {
        return
            from conversation in _context.Conversations.AsNoTracking()
            join listing in _context.Listings.AsNoTracking()
                on conversation.ListingId equals listing.ListingId
            join tenant in _context.Users.AsNoTracking()
                on conversation.TenantId equals tenant.UserId
            join landlord in _context.Users.AsNoTracking()
                on conversation.LandlordId equals landlord.UserId
            select new ConversationSummaryRow
            {
                ConversationId = conversation.ConversationId,
                ListingId = listing.ListingId,
                ListingTitle = listing.Title,
                ListingStatus = listing.Status,
                ListingPrimaryImageUrl = _context.ListingImages
                    .AsNoTracking()
                    .Where(image =>
                        image.ListingId == listing.ListingId &&
                        image.IsPrimary)
                    .Select(image => "/api/Listings/" + image.ListingId + "/images/" + image.ImageId + "/content")
                    .FirstOrDefault(),
                TenantId = tenant.UserId,
                TenantName = tenant.FullName,
                LandlordId = landlord.UserId,
                LandlordName = landlord.FullName,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                LastMessageContent = _context.Messages
                    .AsNoTracking()
                    .Where(m => m.ConversationId == conversation.ConversationId)
                    .OrderByDescending(m => m.SentAt)
                    .ThenByDescending(m => m.MessageId)
                    .Select(m => m.Content)
                    .FirstOrDefault(),

                // The viewer is unknown in the projection, so both counts are
                // computed and the caller's is selected when mapping.
                UnreadForTenantCount = _context.Messages
                    .AsNoTracking()
                    .Count(m =>
                        m.ConversationId == conversation.ConversationId &&
                        !m.IsRead &&
                        m.SenderId != conversation.TenantId),

                UnreadForLandlordCount = _context.Messages
                    .AsNoTracking()
                    .Count(m =>
                        m.ConversationId == conversation.ConversationId &&
                        !m.IsRead &&
                        m.SenderId != conversation.LandlordId)
            };
    }

    private static ConversationSummaryResponse MapSummary(
        ConversationSummaryRow row,
        int viewerId)
    {
        var viewerIsTenant = row.TenantId == viewerId;

        return new ConversationSummaryResponse
        {
            ConversationId = row.ConversationId,
            ListingId = row.ListingId,
            ListingTitle = row.ListingTitle,
            ListingPrimaryImageUrl = row.ListingPrimaryImageUrl,
            ListingStatus = row.ListingStatus,
            OtherPartyName = viewerIsTenant
                ? row.LandlordName
                : row.TenantName,
            OtherPartyId = viewerIsTenant
                ? row.LandlordId
                : row.TenantId,
            OtherPartyIsLandlord = !viewerIsTenant,
            LastMessageContent = row.LastMessageContent,
            LastMessageAt = row.LastMessageAt.AsUtc(),
            UnreadCount = viewerIsTenant
                ? row.UnreadForTenantCount
                : row.UnreadForLandlordCount,
            CreatedAt = row.CreatedAt.AsUtc()
        };
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;

        return sqlException is not null &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private sealed class ConversationHeaderRow
    {
        public int ConversationId { get; set; }

        public int ListingId { get; set; }

        public string ListingTitle { get; set; } = string.Empty;

        public string ListingStatus { get; set; } = string.Empty;

        public string? ListingPrimaryImageUrl { get; set; }

        public int TenantId { get; set; }

        public string TenantName { get; set; } = string.Empty;

        public bool TenantIsActive { get; set; }

        public int LandlordId { get; set; }

        public string LandlordName { get; set; } = string.Empty;

        public bool LandlordIsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastMessageAt { get; set; }
    }

    private sealed class ConversationSummaryRow
    {
        public int ConversationId { get; set; }

        public int ListingId { get; set; }

        public string ListingTitle { get; set; } = string.Empty;

        public string ListingStatus { get; set; } = string.Empty;

        public string? ListingPrimaryImageUrl { get; set; }

        public int TenantId { get; set; }

        public string TenantName { get; set; } = string.Empty;

        public int LandlordId { get; set; }

        public string LandlordName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? LastMessageAt { get; set; }

        public string? LastMessageContent { get; set; }

        public int UnreadForTenantCount { get; set; }

        public int UnreadForLandlordCount { get; set; }
    }
}
