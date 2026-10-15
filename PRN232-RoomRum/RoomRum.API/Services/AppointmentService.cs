using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Appointments;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

/// <summary>
/// Viewing appointment workflow.
///
/// The database (v5.0 Hardened Baseline) is the source of truth for the
/// business rules: it rejects invalid transitions, overlapping slots, past
/// schedules, deposit mismatches and unresolvable listings. This service
/// performs the same checks first so the client receives a clear message
/// instead of a raw SQL error, and so concurrency conflicts surface as a
/// friendly 409.
/// </summary>
public sealed class AppointmentService : IAppointmentService
{
    private readonly RoomRumDbContext _context;
    private readonly IConversationService _conversationService;

    public AppointmentService(
        RoomRumDbContext context,
        IConversationService conversationService)
    {
        _context = context;
        _conversationService = conversationService;
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<CreateAppointmentResponse> CreateAsync(
        int tenantId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var scheduledAtUtc = request.ScheduledAt.ToUtcInstant();

        // Truncate to a whole second before comparing: the column is
        // DATETIME2(0), so a value with a fraction would be rounded on write
        // and could land outside the window the caller intended.
        scheduledAtUtc = scheduledAtUtc.ToWholeSecond();

        if (scheduledAtUtc <= DateTimeExtensions.UtcNowToSecond())
        {
            throw new ArgumentException(
                "The viewing time must be in the future.");
        }

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
                "Only an active Tenant account can book a viewing.");
        }

        var listing = await _context.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.ListingId == request.ListingId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Listing does not exist.");

        if (listing.Status != ListingStatuses.Active)
        {
            throw new InvalidOperationException(
                "Only an Active listing can receive viewing appointments.");
        }

        if (listing.LandlordId == tenantId)
        {
            throw new ArgumentException(
                "You cannot book a viewing for your own listing.");
        }

        // ------------------------------------------------------------
        // Overlap guard. The database trigger performs the same check with
        // UPDLOCK/HOLDLOCK to stay race-free; this pre-check only produces a
        // clearer message and avoids a needless round trip.
        // ------------------------------------------------------------
        var scheduledEndUtc = scheduledAtUtc.AddMinutes(
            request.DurationMinutes);

        var overlaps = await _context.Appointments
            .AsNoTracking()
            .AnyAsync(
                a =>
                    a.ListingId == request.ListingId &&
                    AppointmentStatuses.Unresolved.Contains(a.Status) &&
                    a.ScheduledAt < scheduledEndUtc &&
                    a.ScheduledEndAt > scheduledAtUtc,
                cancellationToken);

        if (overlaps)
        {
            throw new InvalidOperationException(
                "The requested viewing time overlaps another pending or " +
                "confirmed appointment for this listing. Pick a different time.");
        }

        var appointment = new Appointment
        {
            ListingId = listing.ListingId,
            TenantId = tenantId,
            ScheduledAt = scheduledAtUtc,
            DurationMinutes = request.DurationMinutes,

            // Frozen from the listing; the trigger rejects any mismatch.
            DepositAmountSnapshot = listing.DepositAmount,

            Note = NormalizeNote(request.Note),
            Status = AppointmentStatuses.Pending,

            // Explicit whole-second CreatedAt keeps CK_Appointments_UpdatedAt
            // satisfiable when the appointment is updated in the same second.
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The viewing appointment could not be created. " +
                "The listing may have just been reserved or the time slot " +
                "was taken by another tenant.",
                exception);
        }

        // A conversation is opened automatically so Tenant and Landlord can
        // discuss the viewing without an extra user action.
        var conversation = await _conversationService
            .EnsureConversationAsync(
                tenantId,
                listing.ListingId,
                cancellationToken);

        var created = await BuildResponseAsync(
            appointment.AppointmentId,
            tenantId,
            UserRoles.Tenant,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The appointment was created but could not be loaded.");

        return new CreateAppointmentResponse
        {
            Appointment = created,
            ConversationId = conversation.ConversationId
        };
    }

    // ============================================================
    // READ - CALLER'S OWN APPOINTMENTS
    // ============================================================

    public async Task<PagedResult<AppointmentResponse>> GetMineAsync(
        int userId,
        string role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var normalizedStatus = NormalizeStatusFilter(status);

        var query = _context.Appointments
            .AsNoTracking()
            .Where(a => role == UserRoles.Landlord
                ? a.Listing!.LandlordId == userId
                : a.TenantId == userId);

        if (normalizedStatus is not null)
        {
            query = query.Where(a => a.Status == normalizedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var appointments = await query
            .OrderByDescending(a => a.ScheduledAt)
            .ThenByDescending(a => a.AppointmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => a.AppointmentId)
            .ToListAsync(cancellationToken);

        var items = await BuildResponsesAsync(
            appointments,
            userId,
            role,
            cancellationToken);

        return PagedResult<AppointmentResponse>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }

    // ============================================================
    // READ - APPOINTMENTS FOR ONE LANDLORD LISTING
    // ============================================================

    public async Task<PagedResult<AppointmentResponse>> GetForListingAsync(
        int landlordId,
        int listingId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var ownsListing = await _context.Listings
            .AsNoTracking()
            .AnyAsync(
                l => l.ListingId == listingId && l.LandlordId == landlordId,
                cancellationToken);

        if (!ownsListing)
        {
            throw new KeyNotFoundException(
                "Listing not found or does not belong to you.");
        }

        var normalizedStatus = NormalizeStatusFilter(status);

        var query = _context.Appointments
            .AsNoTracking()
            .Where(a => a.ListingId == listingId);

        if (normalizedStatus is not null)
        {
            query = query.Where(a => a.Status == normalizedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var appointments = await query
            .OrderByDescending(a => a.ScheduledAt)
            .ThenByDescending(a => a.AppointmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => a.AppointmentId)
            .ToListAsync(cancellationToken);

        var items = await BuildResponsesAsync(
            appointments,
            landlordId,
            UserRoles.Landlord,
            cancellationToken);

        return PagedResult<AppointmentResponse>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }

    // ============================================================
    // READ - SINGLE APPOINTMENT
    // ============================================================

    public async Task<AppointmentResponse?> GetByIdAsync(
        int userId,
        string role,
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        // Only the two participants may read an appointment: the response
        // includes the other party's phone number and the tenant's private
        // note. Returning null keeps the endpoint indistinguishable from a
        // missing id (404) so it cannot be used to enumerate other people's
        // appointments. This mirrors how payments are scoped; the update path
        // below still uses 403 because that request is an explicit action.
        var participants = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentId == appointmentId)
            .Select(a => new
            {
                a.TenantId,
                LandlordId = a.Listing!.LandlordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (participants is null ||
            (participants.TenantId != userId &&
             participants.LandlordId != userId))
        {
            return null;
        }

        var response = await BuildResponseAsync(
            appointmentId,
            userId,
            role,
            cancellationToken);

        return response;
    }

    // ============================================================
    // UPDATE STATUS
    // ============================================================

    public async Task<AppointmentResponse?> UpdateStatusAsync(
        int userId,
        string role,
        int appointmentId,
        string targetStatus,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetStatus))
        {
            throw new ArgumentException("Status is required.");
        }

        targetStatus = targetStatus.Trim();

        if (targetStatus is not (
            AppointmentStatuses.Confirmed or
            AppointmentStatuses.Rejected or
            AppointmentStatuses.Cancelled or
            AppointmentStatuses.Completed))
        {
            throw new ArgumentException(
                "Status must be Confirmed, Rejected, Cancelled or Completed.");
        }

        // Tracked entity: we must load the listing owner to authorize.
        var appointment = await _context.Appointments
            .Include(a => a.Listing)
            .Include(a => a.Payment)
            .FirstOrDefaultAsync(
                a => a.AppointmentId == appointmentId,
                cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        var listing = appointment.Listing;

        var isTenant = appointment.TenantId == userId;
        var isLandlord = listing is not null && listing.LandlordId == userId;

        if (!isTenant && !isLandlord)
        {
            throw new UnauthorizedAccessException(
                "You are not a participant of this appointment.");
        }

        // The caller's role in this appointment decides which transitions are
        // legal, regardless of the JWT role claim.
        var effectiveRole = isTenant ? UserRoles.Tenant : UserRoles.Landlord;

        if (!AppointmentStatusRules.IsTransitionAllowed(
                appointment.Status,
                effectiveRole,
                targetStatus))
        {
            throw new InvalidOperationException(
                $"An appointment in status '{appointment.Status}' cannot be " +
                $"moved to '{targetStatus}' by a {effectiveRole}.");
        }

        // ------------------------------------------------------------
        // Extra guards that mirror the database triggers so the client gets a
        // precise reason instead of a raw SQL error.
        // ------------------------------------------------------------

        if (targetStatus == AppointmentStatuses.Confirmed)
        {
            if (listing is null || listing.Status != ListingStatuses.Active)
            {
                throw new InvalidOperationException(
                    "Only an Active listing can have a Confirmed appointment.");
            }

            if (appointment.ScheduledAt <= DateTimeExtensions.UtcNowToSecond())
            {
                throw new InvalidOperationException(
                    "A past viewing time cannot be confirmed.");
            }

            var hasConfirmed = await _context.Appointments
                .AsNoTracking()
                .AnyAsync(
                    a =>
                        a.ListingId == appointment.ListingId &&
                        a.AppointmentId != appointment.AppointmentId &&
                        a.Status == AppointmentStatuses.Confirmed,
                    cancellationToken);

            if (hasConfirmed)
            {
                throw new InvalidOperationException(
                    "This listing already has a Confirmed appointment.");
            }
        }

        if (targetStatus == AppointmentStatuses.Completed)
        {
            if (appointment.ScheduledEndAt.HasValue &&
                appointment.ScheduledEndAt.Value >
                DateTimeExtensions.UtcNowToSecond())
            {
                throw new InvalidOperationException(
                    "The viewing cannot be completed before its scheduled " +
                    "end time.");
            }
        }

        if (targetStatus == AppointmentStatuses.Cancelled &&
            appointment.Payment?.PaymentStatus == PaymentStatuses.Paid)
        {
            throw new InvalidOperationException(
                "A paid appointment cannot be cancelled.");
        }

        appointment.Status = targetStatus;
        appointment.UpdatedAt = DateTimeExtensions.UtcNowToSecond();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The appointment status change was rejected. " +
                "Reload the appointment and try again.",
                exception);
        }

        return await BuildResponseAsync(
            appointment.AppointmentId,
            userId,
            role,
            cancellationToken);
    }

    // ============================================================
    // RESPONSE BUILDING
    // ============================================================

    private async Task<List<AppointmentResponse>> BuildResponsesAsync(
        List<int> appointmentIds,
        int viewerId,
        string viewerRole,
        CancellationToken cancellationToken)
    {
        if (appointmentIds.Count == 0)
        {
            return [];
        }

        var rows = await QueryProjection()
            .Where(row => appointmentIds.Contains(row.AppointmentId))
            .ToListAsync(cancellationToken);

        var ordered = appointmentIds
            .Select(id => rows.FirstOrDefault(r => r.AppointmentId == id))
            .Where(row => row is not null)
            .Select(row => Map(row!, viewerId, viewerRole))
            .ToList();

        return ordered;
    }

    private async Task<AppointmentResponse?> BuildResponseAsync(
        int appointmentId,
        int viewerId,
        string viewerRole,
        CancellationToken cancellationToken)
    {
        var row = await QueryProjection()
            .FirstOrDefaultAsync(
                r => r.AppointmentId == appointmentId,
                cancellationToken);

        return row is null ? null : Map(row, viewerId, viewerRole);
    }

    /// <summary>
    /// Single flat projection joining every table the response needs, so a page
    /// of appointments costs one query instead of N+1.
    /// </summary>
    private IQueryable<AppointmentRow> QueryProjection()
    {
        return
            from appointment in _context.Appointments.AsNoTracking()
            join listing in _context.Listings.AsNoTracking()
                on appointment.ListingId equals listing.ListingId
            join tenant in _context.Users.AsNoTracking()
                on appointment.TenantId equals tenant.UserId
            join landlord in _context.Users.AsNoTracking()
                on listing.LandlordId equals landlord.UserId
            from payment in _context.Payments
                .AsNoTracking()
                .Where(p => p.AppointmentId == appointment.AppointmentId)
                .DefaultIfEmpty()
            select new AppointmentRow
            {
                AppointmentId = appointment.AppointmentId,
                ListingId = listing.ListingId,
                ListingTitle = listing.Title,
                ListingAddressLine = listing.AddressLine,
                ListingDistrict = listing.District,
                ListingCity = listing.City,
                ListingStatus = listing.Status,
                ListingPrice = listing.Price,
                ListingLatitude = listing.Latitude,
                ListingLongitude = listing.Longitude,
                ListingPrimaryImageUrl = _context.ListingImages
                    .AsNoTracking()
                    .Where(image =>
                        image.ListingId == listing.ListingId &&
                        image.IsPrimary)
                    .Select(image => "/api/Listings/" + image.ListingId + "/images/" + image.ImageId + "/content")
                    .FirstOrDefault(),
                LandlordId = landlord.UserId,
                LandlordName = landlord.FullName,
                LandlordPhone = landlord.Phone,
                TenantId = tenant.UserId,
                TenantName = tenant.FullName,
                TenantPhone = tenant.Phone,
                ScheduledAt = appointment.ScheduledAt,
                DurationMinutes = appointment.DurationMinutes,
                ScheduledEndAt = appointment.ScheduledEndAt,
                DepositAmountSnapshot = appointment.DepositAmountSnapshot,
                Note = appointment.Note,
                Status = appointment.Status,
                CreatedAt = appointment.CreatedAt,
                UpdatedAt = appointment.UpdatedAt,
                PaymentId = payment == null ? null : payment.PaymentId,
                PaymentAmount = payment == null ? null : payment.Amount,
                PaymentMethod = payment == null ? null : payment.PaymentMethod,
                PaymentStatus = payment == null ? null : payment.PaymentStatus,
                PaymentTransactionCode =
                    payment == null ? null : payment.TransactionCode,
                PaymentFailureReason =
                    payment == null ? null : payment.FailureReason,
                PaymentPaidAt = payment == null ? null : payment.PaidAt,
                PaymentCreatedAt = payment == null ? null : payment.CreatedAt
            };
    }

    private static AppointmentResponse Map(
        AppointmentRow row,
        int viewerId,
        string viewerRole)
    {
        // The caller's role in this specific appointment decides which actions
        // are legal, so it is derived from the relationship rather than from a
        // JWT claim that may be stale or spoofed.
        var effectiveRole = row.TenantId == viewerId
            ? UserRoles.Tenant
            : UserRoles.Landlord;

        var response = new AppointmentResponse
        {
            AppointmentId = row.AppointmentId,
            ListingId = row.ListingId,
            ListingTitle = row.ListingTitle,
            ListingAddressLine = row.ListingAddressLine,
            ListingDistrict = row.ListingDistrict,
            ListingCity = row.ListingCity,
            ListingPrimaryImageUrl = row.ListingPrimaryImageUrl,
            ListingStatus = row.ListingStatus,
            ListingPrice = row.ListingPrice,
            ListingLatitude = row.ListingLatitude,
            ListingLongitude = row.ListingLongitude,
            TenantId = row.TenantId,
            TenantName = row.TenantName,
            TenantPhone = row.TenantPhone,
            LandlordId = row.LandlordId,
            LandlordName = row.LandlordName,
            LandlordPhone = row.LandlordPhone,
            ScheduledAt = row.ScheduledAt.AsUtc(),
            DurationMinutes = row.DurationMinutes,
            ScheduledEndAt = row.ScheduledEndAt.AsUtc(),
            DepositAmountSnapshot = row.DepositAmountSnapshot,
            Note = row.Note,
            Status = row.Status,
            CreatedAt = row.CreatedAt.AsUtc(),
            UpdatedAt = row.UpdatedAt.AsUtc(),
            AllowedNextStatuses = AppointmentStatusRules
                .AllowedTransitions(row.Status, effectiveRole)
                .ToList()
        };

        if (row.PaymentId.HasValue)
        {
            var paymentStatus = row.PaymentStatus ?? PaymentStatuses.Pending;

            response.Payment = new AppointmentPaymentSummary
            {
                PaymentId = row.PaymentId.Value,
                Amount = row.PaymentAmount ?? row.DepositAmountSnapshot,
                PaymentMethod = row.PaymentMethod ?? string.Empty,
                PaymentStatus = paymentStatus,
                TransactionCode = row.PaymentTransactionCode,
                FailureReason = row.PaymentFailureReason,
                PaidAt = row.PaymentPaidAt.AsUtc(),
                CreatedAt = (row.PaymentCreatedAt ?? row.CreatedAt).AsUtc(),
                CanPay = PaymentActionRules.CanPay(
                    paymentStatus,
                    row.Status),
                CanRetry = PaymentActionRules.CanRetry(paymentStatus),
                CanCancel = PaymentActionRules.CanCancel(paymentStatus)
            };
        }

        return response;
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = status.Trim();

        if (!AppointmentStatuses.Pending.Equals(normalized, StringComparison.Ordinal) &&
            !AppointmentStatuses.Confirmed.Equals(normalized, StringComparison.Ordinal) &&
            !AppointmentStatuses.Rejected.Equals(normalized, StringComparison.Ordinal) &&
            !AppointmentStatuses.Cancelled.Equals(normalized, StringComparison.Ordinal) &&
            !AppointmentStatuses.Completed.Equals(normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Status filter must be Pending, Confirmed, Rejected, " +
                "Cancelled or Completed.");
        }

        return normalized;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();

        if (trimmed.Length > 500)
        {
            throw new ArgumentException(
                "Note must not exceed 500 characters.");
        }

        return trimmed;
    }

    /// <summary>
    /// Flat row shape used by the projection above.
    /// </summary>
    private sealed class AppointmentRow
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

        public DateTime ScheduledAt { get; set; }

        public short DurationMinutes { get; set; }

        public DateTime? ScheduledEndAt { get; set; }

        public decimal DepositAmountSnapshot { get; set; }

        public string? Note { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? PaymentId { get; set; }

        public decimal? PaymentAmount { get; set; }

        public string? PaymentMethod { get; set; }

        public string? PaymentStatus { get; set; }

        public string? PaymentTransactionCode { get; set; }

        public string? PaymentFailureReason { get; set; }

        public DateTime? PaymentPaidAt { get; set; }

        public DateTime? PaymentCreatedAt { get; set; }
    }
}
