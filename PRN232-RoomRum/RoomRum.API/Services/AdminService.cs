using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Admin;
using RoomRum.API.DTOs.Listings;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

public class AdminService : IAdminService
{
    private readonly RoomRumDbContext _context;
    private readonly IListingService _listingService;

    public AdminService(
        RoomRumDbContext context,
        IListingService listingService)
    {
        _context = context;
        _listingService = listingService;
    }

    // ============================================================
    // LISTING MODERATION
    // ============================================================

    public Task<ListingPagedResponse> GetListingsAsync(
        string? reviewStatus,
        string? status,
        int page,
        int pageSize)
    {
        // Validation and the landlord/image joins live in ListingService so
        // the admin table and the public catalogue cannot drift apart.
        return _listingService.GetAdminListingsAsync(
            reviewStatus,
            status,
            page,
            pageSize);
    }

    public Task<ListingResponse?> GetListingAsync(int listingId)
    {
        return _listingService.GetAdminListingByIdAsync(listingId);
    }

    public async Task<ListingResponse?> ApproveListingAsync(
        int adminId,
        int listingId)
    {
        var listing = await GetPendingListingAsync(listingId);

        if (listing == null)
        {
            return null;
        }

        var now = DateTimeExtensions.UtcNowToSecond();

        listing.ReviewStatus = ReviewStatuses.Approved;
        listing.Status = ListingStatuses.Active;
        listing.ReviewedByUserId = adminId;
        listing.ReviewedAt = now;
        listing.RejectReason = null;
        listing.ReviewComment = null;
        listing.UpdatedAt = now;
        _context.ListingReviewEvents.Add(new ListingReviewEvent
        {
            ListingId = listingId, ActorUserId = adminId, EventType = "Approved", CreatedAt = now
        });

        await SaveReviewDecisionAsync();

        return await _listingService.GetAdminListingByIdAsync(listingId);
    }

    public async Task<ListingResponse?> RejectListingAsync(
        int adminId,
        int listingId,
        string reason)
    {
        var trimmedReason = reason?.Trim() ?? string.Empty;

        if (trimmedReason.Length < 5 || trimmedReason.Length > 500)
        {
            throw new ArgumentException(
                "A rejection reason must contain between 5 and 500 characters.");
        }

        var listing = await GetPendingListingAsync(listingId);

        if (listing == null)
        {
            return null;
        }

        var now = DateTimeExtensions.UtcNowToSecond();

        listing.ReviewStatus = ReviewStatuses.Rejected;
        listing.ReviewedByUserId = adminId;
        listing.ReviewedAt = now;
        listing.RejectReason = trimmedReason;
        listing.ReviewComment = null;
        listing.UpdatedAt = now;
        _context.ListingReviewEvents.Add(new ListingReviewEvent
        {
            ListingId = listingId, ActorUserId = adminId, EventType = "Rejected",
            Comment = trimmedReason, CreatedAt = now
        });

        await SaveReviewDecisionAsync();

        return await _listingService.GetAdminListingByIdAsync(listingId);
    }

    public async Task<ListingResponse?> RequestListingChangesAsync(
        int adminId,
        int listingId,
        string comment)
    {
        var trimmed = comment?.Trim() ?? string.Empty;
        if (trimmed.Length is < 5 or > 500)
            throw new ArgumentException("A change request comment must contain between 5 and 500 characters.");

        var listing = await GetPendingListingAsync(listingId);
        if (listing == null) return null;

        var now = DateTimeExtensions.UtcNowToSecond();
        listing.ReviewStatus = ReviewStatuses.ChangesRequested;
        listing.ReviewedByUserId = adminId;
        listing.ReviewedAt = now;
        listing.RejectReason = null;
        listing.ReviewComment = trimmed;
        listing.UpdatedAt = now;
        _context.ListingReviewEvents.Add(new ListingReviewEvent
        {
            ListingId = listingId, ActorUserId = adminId, EventType = "ChangesRequested",
            Comment = trimmed, CreatedAt = now
        });
        await SaveReviewDecisionAsync();
        return await _listingService.GetAdminListingByIdAsync(listingId);
    }

    private async Task<Listing?> GetPendingListingAsync(int listingId)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.ListingId == listingId);

        if (listing == null)
        {
            return null;
        }

        if (listing.Status == ListingStatuses.Deleted)
        {
            throw new InvalidOperationException(
                "A deleted listing cannot be reviewed.");
        }

        if (listing.ReviewStatus != ReviewStatuses.PendingReview)
        {
            throw new InvalidOperationException(
                "Only a listing in PendingReview can be approved or rejected. " +
                $"Current review status: '{listing.ReviewStatus}'.");
        }

        return listing;
    }

    private async Task SaveReviewDecisionAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Trigger 52124 (reviewer must be an active Admin) and 52111
            // (listing owner must be an active Landlord) land here.
            throw new InvalidOperationException(
                "The review decision could not be saved because a database " +
                "business rule was violated.",
                ex);
        }
    }

    // ============================================================
    // STATISTICS
    // ============================================================

    public async Task<AdminStatsResponse> GetStatsAsync()
    {
        var stats = new AdminStatsResponse
        {
            TotalUsers = await _context.Users.CountAsync(),
            ActiveUsers = await _context.Users.CountAsync(u => u.IsActive),
            TenantCount = await _context.Users.CountAsync(
                u => u.Role == UserRoles.Tenant),
            LandlordCount = await _context.Users.CountAsync(
                u => u.Role == UserRoles.Landlord),
            AdminCount = await _context.Users.CountAsync(
                u => u.Role == UserRoles.Admin),

            TotalListings = await _context.Listings.CountAsync(),
            ActiveListings = await _context.Listings.CountAsync(
                l => l.Status == ListingStatuses.Active),
            HiddenListings = await _context.Listings.CountAsync(
                l => l.Status == ListingStatuses.Hidden),
            ReservedListings = await _context.Listings.CountAsync(
                l => l.Status == ListingStatuses.Reserved),
            RentedListings = await _context.Listings.CountAsync(
                l => l.Status == ListingStatuses.Rented),
            DeletedListings = await _context.Listings.CountAsync(
                l => l.Status == ListingStatuses.Deleted),

            PendingReviewListings = await _context.Listings.CountAsync(
                l => l.ReviewStatus == ReviewStatuses.PendingReview),
            ApprovedListings = await _context.Listings.CountAsync(
                l => l.ReviewStatus == ReviewStatuses.Approved),
            RejectedListings = await _context.Listings.CountAsync(
                l => l.ReviewStatus == ReviewStatuses.Rejected),

            TotalAppointments = await _context.Appointments.CountAsync(),
            PendingAppointments = await _context.Appointments.CountAsync(
                a => a.Status == AppointmentStatuses.Pending),
            ConfirmedAppointments = await _context.Appointments.CountAsync(
                a => a.Status == AppointmentStatuses.Confirmed),
            CompletedAppointments = await _context.Appointments.CountAsync(
                a => a.Status == AppointmentStatuses.Completed),
            CancelledAppointments = await _context.Appointments.CountAsync(
                a => a.Status == AppointmentStatuses.Cancelled),
            RejectedAppointments = await _context.Appointments.CountAsync(
                a => a.Status == AppointmentStatuses.Rejected),

            TotalPayments = await _context.Payments.CountAsync(),
            PaidPayments = await _context.Payments.CountAsync(
                p => p.PaymentStatus == PaymentStatuses.Paid),
            PaidRevenue = await _context.Payments
                .Where(p => p.PaymentStatus == PaymentStatuses.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m,

            TotalConversations = await _context.Conversations.CountAsync(),
            TotalMessages = await _context.Messages.CountAsync()
        };

        return stats;
    }

    // ============================================================
    // ACCOUNTS
    // ============================================================

    public async Task<AdminUserPagedResponse> GetUsersAsync(
        string? role,
        bool? isActive,
        string? search,
        int page,
        int pageSize)
    {
        PaginationRules.Validate(page, pageSize);

        if (!string.IsNullOrWhiteSpace(role))
        {
            role = role.Trim();

            if (role != UserRoles.Tenant &&
                role != UserRoles.Landlord &&
                role != UserRoles.Admin)
            {
                throw new ArgumentException(
                    "role must be one of: Tenant, Landlord, Admin.");
            }
        }
        else
        {
            role = null;
        }

        search = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        if (search is { Length: > 100 })
        {
            throw new ArgumentException(
                "search must not be longer than 100 characters.");
        }

        var query = _context.Users
            .AsNoTracking()
            .AsQueryable();

        if (role != null)
        {
            query = query.Where(u => u.Role == role);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (search != null)
        {
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                u.Email.Contains(search) ||
                (u.Phone != null && u.Phone.Contains(search)));
        }

        var totalItems = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.UserId).ToList();

        var listingCounts = await _context.Listings
            .AsNoTracking()
            .Where(l => userIds.Contains(l.LandlordId))
            .GroupBy(l => l.LandlordId)
            .Select(g => new { LandlordId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(
                x => x.LandlordId,
                x => x.Count);

        return new AdminUserPagedResponse
        {
            Items = users
                .Select(u => MapUser(
                    u,
                    listingCounts.TryGetValue(u.UserId, out var count)
                        ? count
                        : 0))
                .ToList(),

            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminUserResponse?> SetUserActiveAsync(
        int targetUserId,
        bool isActive)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == targetUserId);

        if (user == null)
        {
            return null;
        }

        // No admin may lock any Admin account through this endpoint, which
        // also stops an admin from locking themselves out.
        if (user.Role == UserRoles.Admin)
        {
            throw new InvalidOperationException(
                "Admin accounts cannot be locked or unlocked through this endpoint.");
        }

        // Calling lock on an already locked account (or unlock on an active
        // one) is a no-op, not an error.
        if (user.IsActive == isActive)
        {
            return await BuildUserResponseAsync(user);
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTimeExtensions.UtcNowToSecond();

        if (!isActive)
        {
            // Locking ends the sessions: every refresh token the account still
            // holds is revoked. The 60-minute access token expires on its own
            // and every write path re-checks IsActive (or a trigger does).
            var revokedAt = DateTimeExtensions.UtcNowToSecond();

            var activeTokens = await _context.RefreshTokens
                .Where(t =>
                    t.UserId == targetUserId &&
                    t.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = revokedAt;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "The account status could not be changed because a database " +
                "business rule was violated.",
                ex);
        }

        return await BuildUserResponseAsync(user);
    }

    private async Task<AdminUserResponse> BuildUserResponseAsync(User user)
    {
        var listingCount = await _context.Listings
            .AsNoTracking()
            .CountAsync(l => l.LandlordId == user.UserId);

        return MapUser(user, listingCount);
    }

    private static AdminUserResponse MapUser(User user, int listingCount)
    {
        return new AdminUserResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            ListingCount = listingCount,
            CreatedAt = user.CreatedAt
        };
    }
}
