using RoomRum.API.DTOs.Admin;
using RoomRum.API.DTOs.Listings;

namespace RoomRum.API.Services;

/// <summary>
/// Admin operations (v5.2): listing review, statistics and account lock.
/// Every method assumes the caller already passed [Authorize(Roles = "Admin")];
/// the service re-checks data-level rules (e.g. reviewers must be Admins, and
/// the database trigger TR_Listings_ReviewIntegrity enforces it again).
/// </summary>
public interface IAdminService
{
    Task<ListingPagedResponse> GetListingsAsync(
        string? reviewStatus,
        string? status,
        int page,
        int pageSize);

    Task<ListingResponse?> GetListingAsync(int listingId);

    Task<ListingResponse?> ApproveListingAsync(
        int adminId,
        int listingId);

    Task<ListingResponse?> RejectListingAsync(
        int adminId,
        int listingId,
        string reason);

    Task<ListingResponse?> RequestListingChangesAsync(
        int adminId,
        int listingId,
        string comment);

    Task<AdminStatsResponse> GetStatsAsync();

    Task<AdminUserPagedResponse> GetUsersAsync(
        string? role,
        bool? isActive,
        string? search,
        int page,
        int pageSize);

    /// <summary>
    /// Locks (isActive = false) or unlocks an account. Locking revokes every
    /// refresh token the account still holds. Admin accounts are never
    /// lockable through this endpoint; returns null for an unknown user.
    /// </summary>
    Task<AdminUserResponse?> SetUserActiveAsync(
        int targetUserId,
        bool isActive);
}
