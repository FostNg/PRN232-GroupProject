using RoomRum.API.DTOs.Listings;

namespace RoomRum.API.Services;

public interface IListingService
{
    /// <summary>
    /// Public browse/search. Only listings a Tenant can act on are returned
    /// (Active and Reserved); Reserved listings stay visible so the map and
    /// search results do not develop holes, but they are marked unavailable.
    /// </summary>
    Task<ListingPagedResponse> GetPublicListingsAsync(
        ListingSearchQuery query);

    /// <summary>
    /// Listing detail for a public viewer. A Landlord previewing their own
    /// listing may pass their user id to also see it while Hidden, so the
    /// "preview before publishing" flow works from the mobile app.
    /// </summary>
    Task<ListingResponse?> GetPublicListingByIdAsync(
        int listingId,
        int? viewerUserId = null);

    Task<ListingPagedResponse> GetMyListingsAsync(
        int landlordId,
        int page,
        int pageSize);

    /// <summary>
    /// Admin moderation list (v5.2). Every lifecycle and review status is
    /// visible; ?reviewStatus=PendingReview returns the queue, oldest first.
    /// </summary>
    Task<ListingPagedResponse> GetAdminListingsAsync(
        string? reviewStatus,
        string? status,
        int page,
        int pageSize);

    /// <summary>
    /// Admin listing detail (v5.2). Unlike the public detail it returns the
    /// listing in any status, including Hidden and Deleted.
    /// </summary>
    Task<ListingResponse?> GetAdminListingByIdAsync(int listingId);

    Task<ListingResponse> CreateListingAsync(
        int landlordId,
        CreateListingRequest request);

    Task<ListingResponse?> SubmitListingAsync(int landlordId, int listingId);

    Task<ListingResponse?> UpdateListingAsync(
        int landlordId,
        int listingId,
        UpdateListingRequest request);

    Task<ListingResponse?> UpdateListingStatusAsync(
        int landlordId,
        int listingId,
        string status);

    Task<bool> DeleteImageAsync(
        int landlordId,
        int listingId,
        int imageId);

    Task<bool> SetPrimaryImageAsync(
        int landlordId,
        int listingId,
        int imageId);
}
