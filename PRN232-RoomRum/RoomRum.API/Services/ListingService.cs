using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Listings;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

public class ListingService : IListingService
{
    private readonly RoomRumDbContext _context;

    public ListingService(RoomRumDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // PUBLIC LISTINGS
    // ============================================================

    public async Task<ListingPagedResponse> GetPublicListingsAsync(
        ListingSearchQuery searchQuery)
    {
        PaginationRules.Validate(searchQuery.Page, searchQuery.PageSize);

        if (!ListingSortOptions.IsValid(searchQuery.Sort))
        {
            throw new ArgumentException(
                "Sort must be one of: " +
                $"{string.Join(", ", ListingSortOptions.All)}.");
        }

        ValidateRanges(searchQuery);

        // Reserved listings stay visible: the database still allows browsing
        // and favouriting them, so hiding them would make search results and
        // the map develop unexplained holes. They are reported as unavailable
        // instead so the client can grey them out.
        var listingsQuery = _context.Listings
            .AsNoTracking()
            .Where(l =>
                l.Status == ListingStatuses.Active ||
                l.Status == ListingStatuses.Reserved);

        listingsQuery = ApplyFilters(listingsQuery, searchQuery);


        // ------------------------------------------------------------
        // Radius search (map screen)
        // ------------------------------------------------------------
        // The schema stores coordinates as DECIMAL(9,6) and has no geography
        // column, so EF Core cannot translate a haversine expression into SQL.
        // A radius search therefore runs in two steps: a rectangular bounding
        // box that SQL evaluates using the existing index, then an exact
        // distance check in memory over that already-bounded candidate set.
        if (searchQuery.HasRadiusSearch)
        {
            return await SearchByRadiusAsync(listingsQuery, searchQuery);
        }

        var sorted = ApplySort(listingsQuery, searchQuery.Sort);

        var totalItems = await sorted.CountAsync();

        var listings = await sorted
            .Skip((searchQuery.Page - 1) * searchQuery.PageSize)
            .Take(searchQuery.PageSize)
            .ToListAsync();

        return await BuildPagedResponseAsync(
            listings,
            searchQuery.Page,
            searchQuery.PageSize,
            totalItems);
    }

    // ============================================================
    // FILTERS
    // ============================================================

    private static IQueryable<Listing> ApplyFilters(
        IQueryable<Listing> query,
        ListingSearchQuery filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim();

            query = query.Where(l =>
                l.Title.Contains(search) ||
                (l.Description != null &&
                 l.Description.Contains(search)) ||
                l.AddressLine.Contains(search) ||
                (l.Ward != null &&
                 l.Ward.Contains(search)) ||
                l.District.Contains(search) ||
                l.City.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filters.City))
        {
            var city = filters.City.Trim();

            query = query.Where(l => l.City == city);
        }

        if (!string.IsNullOrWhiteSpace(filters.District))
        {
            var district = filters.District.Trim();

            query = query.Where(l => l.District == district);
        }

        if (!string.IsNullOrWhiteSpace(filters.Ward))
        {
            var ward = filters.Ward.Trim();

            query = query.Where(l => l.Ward == ward);
        }

        if (filters.MinPrice.HasValue)
        {
            query = query.Where(l => l.Price >= filters.MinPrice.Value);
        }

        if (filters.MaxPrice.HasValue)
        {
            query = query.Where(l => l.Price <= filters.MaxPrice.Value);
        }

        if (filters.MinArea.HasValue)
        {
            query = query.Where(l => l.AreaSqm >= filters.MinArea.Value);
        }

        if (filters.MaxArea.HasValue)
        {
            query = query.Where(l => l.AreaSqm <= filters.MaxArea.Value);
        }

        if (filters.MinBedrooms.HasValue)
        {
            query = query.Where(l => l.Bedrooms >= filters.MinBedrooms.Value);
        }

        if (filters.MinBathrooms.HasValue)
        {
            query = query.Where(l => l.Bathrooms >= filters.MinBathrooms.Value);
        }

        // Amenities are "must have" filters: only true narrows the result set,
        // while null and false leave it untouched.
        if (filters.HasWifi == true)
        {
            query = query.Where(l => l.HasWifi);
        }

        if (filters.HasAirConditioner == true)
        {
            query = query.Where(l => l.HasAirConditioner);
        }

        if (filters.HasParking == true)
        {
            query = query.Where(l => l.HasParking);
        }

        if (filters.HasKitchen == true)
        {
            query = query.Where(l => l.HasKitchen);
        }

        if (filters.HasPrivateBathroom == true)
        {
            query = query.Where(l => l.HasPrivateBathroom);
        }

        return query;
    }

    // ============================================================
    // SORTING
    // ============================================================

    private static IQueryable<Listing> ApplySort(
        IQueryable<Listing> query,
        string sort)
    {
        return sort.ToLowerInvariant() switch
        {
            ListingSortOptions.PriceAsc =>
                query.OrderBy(l => l.Price)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.PriceDesc =>
                query.OrderByDescending(l => l.Price)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.AreaAsc =>
                query.OrderBy(l => l.AreaSqm)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.AreaDesc =>
                query.OrderByDescending(l => l.AreaSqm)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.DepositAsc =>
                query.OrderBy(l => l.DepositAmount)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.DepositDesc =>
                query.OrderByDescending(l => l.DepositAmount)
                    .ThenByDescending(l => l.CreatedAt),

            ListingSortOptions.AmenitiesDesc =>
                query.OrderByDescending(l =>
                    (l.HasWifi ? 1 : 0) +
                    (l.HasAirConditioner ? 1 : 0) +
                    (l.HasParking ? 1 : 0) +
                    (l.HasKitchen ? 1 : 0) +
                    (l.HasPrivateBathroom ? 1 : 0))
                    .ThenByDescending(l => l.CreatedAt),

            _ => query.OrderByDescending(l => l.CreatedAt)
        };
    }

    // ============================================================
    // RADIUS SEARCH
    // ============================================================

    private async Task<ListingPagedResponse> SearchByRadiusAsync(
        IQueryable<Listing> source,
        ListingSearchQuery searchQuery)
    {
        var radiusKm = (double)searchQuery.RadiusKm!.Value;
        var centerLatitude = (double)searchQuery.CenterLatitude;
        var centerLongitude = (double)searchQuery.CenterLongitude;

        // One degree of latitude is ~111.32 km; a degree of longitude shrinks
        // with latitude. The cosine is clamped so a search at extreme latitudes
        // cannot produce a zero-width box.
        const double kilometresPerDegreeLatitude = 111.32;

        var latitudeDelta = radiusKm / kilometresPerDegreeLatitude;

        var longitudeScale = Math.Cos(centerLatitude * Math.PI / 180.0);

        if (longitudeScale < 0.01)
        {
            longitudeScale = 0.01;
        }

        var longitudeDelta = radiusKm /
            (kilometresPerDegreeLatitude * longitudeScale);

        var minLatitude = (decimal)(centerLatitude - latitudeDelta);
        var maxLatitude = (decimal)(centerLatitude + latitudeDelta);
        var minLongitude = (decimal)(centerLongitude - longitudeDelta);
        var maxLongitude = (decimal)(centerLongitude + longitudeDelta);

        var candidates = await source
            .Where(l =>
                l.Latitude != null &&
                l.Longitude != null &&
                l.Latitude >= minLatitude &&
                l.Latitude <= maxLatitude &&
                l.Longitude >= minLongitude &&
                l.Longitude <= maxLongitude)
            .ToListAsync();

        var withDistance = candidates
            .Select(listing => new
            {
                Listing = listing,
                DistanceKm = HaversineKm(
                    centerLatitude,
                    centerLongitude,
                    (double)listing.Latitude!.Value,
                    (double)listing.Longitude!.Value)
            })
            .Where(item => item.DistanceKm <= radiusKm)
            .OrderBy(item => item.DistanceKm)
            .ToList();

        var pageItems = withDistance
            .Skip((searchQuery.Page - 1) * searchQuery.PageSize)
            .Take(searchQuery.PageSize)
            .ToList();

        var response = await BuildPagedResponseAsync(
            pageItems.Select(item => item.Listing).ToList(),
            searchQuery.Page,
            searchQuery.PageSize,
            withDistance.Count);

        // Attach the computed distance so the client can render "1.2 km away"
        // without repeating the calculation.
        var distances = pageItems.ToDictionary(
            item => item.Listing.ListingId,
            item => item.DistanceKm);

        foreach (var item in response.Items)
        {
            if (distances.TryGetValue(item.ListingId, out var distance))
            {
                item.DistanceKm = Math.Round(distance, 2);
            }
        }

        return response;
    }

    /// <summary>
    /// Great-circle distance in kilometres between two WGS-84 points.
    /// </summary>
    private static double HaversineKm(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        var latitude1Radians = latitude1 * Math.PI / 180.0;
        var latitude2Radians = latitude2 * Math.PI / 180.0;

        var deltaLatitude = (latitude2 - latitude1) * Math.PI / 180.0;
        var deltaLongitude = (longitude2 - longitude1) * Math.PI / 180.0;

        var a =
            Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
            Math.Cos(latitude1Radians) *
            Math.Cos(latitude2Radians) *
            Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);

        return earthRadiusKm *
               2 *
               Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ============================================================
    // RANGE VALIDATION
    // ============================================================

    private static void ValidateRanges(ListingSearchQuery query)
    {
        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            throw new ArgumentException(
                "minPrice cannot be greater than maxPrice.");
        }

        if (query.MinArea.HasValue &&
            query.MaxArea.HasValue &&
            query.MinArea.Value > query.MaxArea.Value)
        {
            throw new ArgumentException(
                "minArea cannot be greater than maxArea.");
        }

        var hasLatitude = query.Latitude.HasValue;
        var hasLongitude = query.Longitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            throw new ArgumentException(
                "Latitude and Longitude must be provided together.");
        }

        if (query.RadiusKm.HasValue)
        {
            if (!hasLatitude)
            {
                throw new ArgumentException(
                    "radiusKm requires both latitude and longitude.");
            }

            if (query.RadiusKm.Value <= 0)
            {
                throw new ArgumentException(
                    "radiusKm must be greater than zero.");
            }

            if (query.RadiusKm.Value > 50)
            {
                throw new ArgumentException(
                    "radiusKm must not exceed 50 km.");
            }
        }
    }

    // ============================================================
    // PUBLIC DETAIL
    // ============================================================

    public async Task<ListingResponse?> GetPublicListingByIdAsync(
        int listingId,
        int? viewerUserId = null)
    {
        // Public viewers only see listings they can act on. The owning Landlord
        // may additionally preview their own Hidden listing so the "check
        // before publishing" flow works from the mobile app.
        var listing = await _context.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.ListingId == listingId &&
                (l.Status == ListingStatuses.Active ||
                 l.Status == ListingStatuses.Reserved ||
                 (viewerUserId != null &&
                  l.LandlordId == viewerUserId &&
                  l.Status != ListingStatuses.Deleted)));

        if (listing == null)
        {
            return null;
        }

        return await BuildListingResponseAsync(
            listing,
            includeReviewHistory: viewerUserId == listing.LandlordId);
    }

    // ============================================================
    // LANDLORD - MY LISTINGS
    // ============================================================

    public async Task<ListingPagedResponse> GetMyListingsAsync(
        int landlordId,
        int page,
        int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Listings
            .AsNoTracking()
            .Where(l =>
                l.LandlordId == landlordId &&
                l.Status != "Deleted")
            .OrderByDescending(l => l.CreatedAt);

        var totalItems = await query.CountAsync();

        var listings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return await BuildPagedResponseAsync(
            listings,
            page,
            pageSize,
            totalItems,
            includeReviewHistory: true);
    }

    // ============================================================
    // ADMIN - MODERATION LIST (v5.2)
    // ============================================================

    public async Task<ListingPagedResponse> GetAdminListingsAsync(
        string? reviewStatus,
        string? status,
        int page,
        int pageSize)
    {
        PaginationRules.Validate(page, pageSize);

        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            reviewStatus = reviewStatus.Trim();

            if (!ReviewStatuses.IsValid(reviewStatus))
            {
                throw new ArgumentException(
                    "reviewStatus must be one of: " +
                    $"{string.Join(", ", ReviewStatuses.All)}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            status = status.Trim();

            if (!ListingStatuses.All.Contains(status, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "status must be one of: " +
                    $"{string.Join(", ", ListingStatuses.All)}.");
            }
        }

        var query = _context.Listings
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            query = query.Where(l => l.ReviewStatus == reviewStatus);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        // The queue view (reviewStatus=PendingReview) is oldest first so
        // nothing is forgotten. Any other filter reads like the regular
        // listing tables, newest first.
        var ordered = reviewStatus == ReviewStatuses.PendingReview
            ? query.OrderBy(l => l.CreatedAt).ThenBy(l => l.ListingId)
            : query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.ListingId);

        var totalItems = await ordered.CountAsync();

        var listings = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return await BuildPagedResponseAsync(
            listings,
            page,
            pageSize,
            totalItems,
            includeReviewHistory: true);
    }

    public async Task<ListingResponse?> GetAdminListingByIdAsync(
        int listingId)
    {
        var listing = await _context.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ListingId == listingId);

        if (listing == null)
        {
            return null;
        }

        return await BuildListingResponseAsync(listing, includeReviewHistory: true);
    }

    // ============================================================
    // CREATE LISTING
    // ============================================================

    public async Task<ListingResponse> CreateListingAsync(
        int landlordId,
        CreateListingRequest request)
    {
        var landlord = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.UserId == landlordId &&
                u.Role == "Landlord" &&
                u.IsActive);

        if (landlord == null)
        {
            throw new InvalidOperationException(
                "The authenticated user is not an active Landlord.");
        }

        ValidateListingValues(
            request.Title,
            request.Description,
            request.AddressLine,
            request.Ward,
            request.District,
            request.City,
            request.Price,
            request.DepositAmount,
            request.AreaSqm,
            request.Bedrooms,
            request.Bathrooms,
            request.ElectricityPrice,
            request.WaterPrice,
            request.ServiceFee,
            request.Latitude,
            request.Longitude);

        var listing = new Listing
        {
            LandlordId = landlordId,

            Title = request.Title.Trim(),

            Description =
                NormalizeNullable(request.Description),

            AddressLine =
                request.AddressLine.Trim(),

            Ward =
                NormalizeNullable(request.Ward),

            District =
                request.District.Trim(),

            City =
                request.City.Trim(),

            Price =
                request.Price,

            DepositAmount =
                request.DepositAmount,

            AreaSqm =
                request.AreaSqm,

            Bedrooms =
                request.Bedrooms,

            Bathrooms =
                request.Bathrooms,

            HasWifi =
                request.HasWifi,

            HasAirConditioner =
                request.HasAirConditioner,

            HasParking =
                request.HasParking,

            HasKitchen =
                request.HasKitchen,

            HasPrivateBathroom =
                request.HasPrivateBathroom,

            ElectricityPrice =
                request.ElectricityPrice,

            WaterPrice =
                request.WaterPrice,

            ServiceFee =
                request.ServiceFee,

            Latitude =
                request.Latitude,

            Longitude =
                request.Longitude,

            // New listings stay private as a database-backed Draft until the
            // landlord confirms the review step and explicitly submits.
            Status = "Hidden",

            ReviewStatus = ReviewStatuses.Draft,

            // Explicit whole-second CreatedAt keeps CK_Listings_UpdatedAt
            // satisfiable when the listing is edited in the same second.
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // ----------------------------------------------------
            // SAVE LISTING FIRST
            // ----------------------------------------------------

            _context.Listings.Add(listing);

            await _context.SaveChangesAsync();

            // Photos are added through the multipart upload endpoint after the listing exists.

            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                "The listing could not be created because a database business rule was violated.",
                ex);
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }

        return await BuildListingResponseAsync(listing);
    }

    public async Task<ListingResponse?> SubmitListingAsync(
        int landlordId,
        int listingId)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(l =>
            l.ListingId == listingId && l.LandlordId == landlordId);
        if (listing == null) return null;

        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            throw new InvalidOperationException("Only a Draft or ChangesRequested listing can be submitted.");

        if (listing.ReviewStatus == ReviewStatuses.ChangesRequested)
        {
            var requestedAt = await _context.ListingReviewEvents
                .Where(e => e.ListingId == listingId && e.EventType == "ChangesRequested")
                .OrderByDescending(e => e.ReviewEventId)
                .Select(e => (DateTime?)e.CreatedAt)
                .FirstOrDefaultAsync();
            if (requestedAt.HasValue && listing.UpdatedAt <= requestedAt.Value)
                throw new InvalidOperationException(
                    "Update the listing details or photos before resubmitting it for review.");
        }

        var imageCount = await _context.ListingImages.CountAsync(i => i.ListingId == listingId);
        var primaryCount = await _context.ListingImages.CountAsync(i => i.ListingId == listingId && i.IsPrimary);
        ValidatePublishRequirements(listing.Description, imageCount, primaryCount);

        var wasRequestedForChanges = listing.ReviewStatus == ReviewStatuses.ChangesRequested;
        var now = DateTimeExtensions.UtcNowToSecond();
        listing.ReviewStatus = ReviewStatuses.PendingReview;
        listing.ReviewedByUserId = null;
        listing.ReviewedAt = null;
        listing.RejectReason = null;
        listing.ReviewComment = null;
        listing.UpdatedAt = now;
        _context.ListingReviewEvents.Add(new ListingReviewEvent
        {
            ListingId = listingId,
            ActorUserId = landlordId,
            EventType = wasRequestedForChanges ? "Resubmitted" : "Submitted",
            CreatedAt = now
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("The listing could not be submitted because its data or review state changed.", ex);
        }

        return await BuildListingResponseAsync(listing, includeReviewHistory: true);
    }

    // ============================================================
    // UPDATE LISTING
    // ============================================================

    public async Task<ListingResponse?> UpdateListingAsync(
        int landlordId,
        int listingId,
        UpdateListingRequest request)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l =>
                l.ListingId == listingId &&
                l.LandlordId == landlordId);

        if (listing == null)
        {
            return null;
        }

        if (listing.Status is "Reserved" or "Rented" or "Deleted")
        {
            throw new InvalidOperationException(
                "This listing cannot be edited in its current status.");
        }
        if (listing.ReviewStatus == ReviewStatuses.Rejected)
            throw new InvalidOperationException("This listing was rejected and cannot be edited or resubmitted.");
        if (listing.ReviewStatus == ReviewStatuses.PendingReview)
            throw new InvalidOperationException("A listing cannot be edited while it is waiting for Admin review.");

        ValidateListingValues(
            request.Title,
            request.Description,
            request.AddressLine,
            request.Ward,
            request.District,
            request.City,
            request.Price,
            request.DepositAmount,
            request.AreaSqm,
            request.Bedrooms,
            request.Bathrooms,
            request.ElectricityPrice,
            request.WaterPrice,
            request.ServiceFee,
            request.Latitude,
            request.Longitude);

        if (listing.Status == "Active")
        {
            var imageCount =
                await _context.ListingImages
                    .CountAsync(i =>
                        i.ListingId == listingId);

            var primaryImageCount =
                await _context.ListingImages
                    .CountAsync(i =>
                        i.ListingId == listingId &&
                        i.IsPrimary);

            ValidatePublishRequirements(
                request.Description,
                imageCount,
                primaryImageCount);
        }

        // Any edit of a Hidden listing invalidates the previous decision: the
        // Admin approved (or rejected) content that no longer exists. Sending
        // it back to PendingReview keeps an approval tied to what was seen.
        listing.Title =
            request.Title.Trim();

        listing.Description =
            NormalizeNullable(request.Description);

        listing.AddressLine =
            request.AddressLine.Trim();

        listing.Ward =
            NormalizeNullable(request.Ward);

        listing.District =
            request.District.Trim();

        listing.City =
            request.City.Trim();

        listing.Price =
            request.Price;

        listing.DepositAmount =
            request.DepositAmount;

        listing.AreaSqm =
            request.AreaSqm;

        listing.Bedrooms =
            request.Bedrooms;

        listing.Bathrooms =
            request.Bathrooms;

        listing.HasWifi =
            request.HasWifi;

        listing.HasAirConditioner =
            request.HasAirConditioner;

        listing.HasParking =
            request.HasParking;

        listing.HasKitchen =
            request.HasKitchen;

        listing.HasPrivateBathroom =
            request.HasPrivateBathroom;

        listing.ElectricityPrice =
            request.ElectricityPrice;

        listing.WaterPrice =
            request.WaterPrice;

        listing.ServiceFee =
            request.ServiceFee;

        listing.Latitude =
            request.Latitude;

        listing.Longitude =
            request.Longitude;

        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "The listing could not be updated because one or more database business rules were violated.",
                ex);
        }

        return await BuildListingResponseAsync(listing);
    }

    // ============================================================
    // UPDATE STATUS
    // ============================================================

    public async Task<ListingResponse?> UpdateListingStatusAsync(
        int landlordId,
        int listingId,
        string status)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l =>
                l.ListingId == listingId &&
                l.LandlordId == landlordId);

        if (listing == null)
        {
            return null;
        }

        status = status.Trim();

        // Rented is the one workflow-reached status a Landlord still closes
        // out: after the deposit is paid (Reserved) and the paid viewing has
        // been Completed, the room is let. Active -> Rented stays invalid,
        // because the booking/payment workflow has to happen first.
        var wantsRented = status == ListingStatuses.Rented;

        if (!wantsRented &&
            status != ListingStatuses.Active &&
            status != ListingStatuses.Hidden &&
            status != ListingStatuses.Deleted)
        {
            throw new ArgumentException(
                "Landlord can only change listing status to Active, Hidden, " +
                "Rented or Deleted.");
        }

        // Rented -> Rented stays idempotent, like every other status with
        // itself; anything else may only be closed out from Reserved.
        if (wantsRented &&
            listing.Status != ListingStatuses.Reserved &&
            listing.Status != ListingStatuses.Rented)
        {
            throw new InvalidOperationException(
                $"A listing in status '{listing.Status}' cannot be marked as " +
                "Rented. Only a Reserved listing can be closed out as Rented.");
        }

        if (!wantsRented &&
            (listing.Status == ListingStatuses.Reserved ||
             listing.Status == ListingStatuses.Rented))
        {
            throw new InvalidOperationException(
                "Reserved or Rented listings are controlled by the " +
                "booking/payment workflow.");
        }

        if (listing.Status == "Deleted")
        {
            throw new InvalidOperationException(
                "A Deleted listing cannot be reopened.");
        }

        if (listing.Status == status)
        {
            return await BuildListingResponseAsync(listing);
        }

        if (wantsRented)
        {
            // Mirrors TR_Listings_Integrity (error 52123) so the Landlord gets
            // the reason instead of a raw trigger message.
            var hasCompletedPaidVisit = await _context.Appointments
                .AsNoTracking()
                .AnyAsync(
                    a =>
                        a.ListingId == listingId &&
                        a.Status == AppointmentStatuses.Completed &&
                        a.Payment != null &&
                        a.Payment.PaymentStatus == PaymentStatuses.Paid);

            if (!hasCompletedPaidVisit)
            {
                throw new InvalidOperationException(
                    "A Reserved listing can be marked as Rented only after the " +
                    "paid viewing has been completed.");
            }
        }

        if (status == ListingStatuses.Active)
        {
            var imageCount =
                await _context.ListingImages
                    .CountAsync(i =>
                        i.ListingId == listingId);

            var primaryCount =
                await _context.ListingImages
                    .CountAsync(i =>
                        i.ListingId == listingId &&
                        i.IsPrimary);

            ValidatePublishRequirements(
                listing.Description,
                imageCount,
                primaryCount);

            // Review gate. Requirements are checked first so the landlord
            // gets a concrete description/image message before moderation.
            if (listing.ReviewStatus != ReviewStatuses.Approved)
            {
                if (listing.ReviewStatus == ReviewStatuses.Rejected &&
                    !string.IsNullOrWhiteSpace(listing.RejectReason))
                {
                    throw new InvalidOperationException(
                        $"This listing was rejected by an Admin: " +
                        $"{listing.RejectReason} Edit the listing to send it " +
                        "back for review.");
                }

                throw new InvalidOperationException(
                    "This listing must be approved by an Admin before it can " +
                    $"be published. Current review status: " +
                    $"'{listing.ReviewStatus}'.");
            }
        }

        listing.Status =
            status;

        listing.UpdatedAt =
            DateTimeExtensions.UtcNowToSecond();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "The listing status change violates a business rule.",
                ex);
        }

        return await BuildListingResponseAsync(listing);
    }

    // ============================================================
    // DELETE IMAGE
    // ============================================================

    public async Task<bool> DeleteImageAsync(
        int landlordId,
        int listingId,
        int imageId)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l =>
                l.ListingId == listingId &&
                l.LandlordId == landlordId);

        if (listing == null)
        {
            return false;
        }

        if (listing.Status is "Reserved" or "Rented" or "Deleted")
        {
            throw new InvalidOperationException(
                "Images cannot be changed for this listing status.");
        }
        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            throw new InvalidOperationException("Photos can only be changed on a Draft or a listing returned for changes.");

        var image =
            await _context.ListingImages
                .FirstOrDefaultAsync(i =>
                    i.ImageId == imageId &&
                    i.ListingId == listingId);

        if (image == null)
        {
            return false;
        }

        var imageCount =
            await _context.ListingImages
                .CountAsync(i =>
                    i.ListingId == listingId);

        if (listing.Status == "Active")
        {
            if (imageCount <= 1)
            {
                throw new InvalidOperationException(
                    "An Active listing must keep at least one image.");
            }

            if (image.IsPrimary)
            {
                throw new InvalidOperationException(
                    "Set another primary image before deleting the current primary image.");
            }
        }

        _context.ListingImages.Remove(image);
        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "The image could not be deleted because a database rule was violated.",
                ex);
        }

        return true;
    }

    // ============================================================
    // SET PRIMARY IMAGE
    // ============================================================

    public async Task<bool> SetPrimaryImageAsync(
        int landlordId,
        int listingId,
        int imageId)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l =>
                l.ListingId == listingId &&
                l.LandlordId == landlordId);

        if (listing == null)
        {
            return false;
        }

        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            throw new InvalidOperationException("Photos can only be changed on a Draft or a listing returned for changes.");

        if (listing.Status is "Reserved" or "Rented" or "Deleted")
        {
            throw new InvalidOperationException(
                "Primary image cannot be changed for this listing status.");
        }

        var imageExists =
            await _context.ListingImages
                .AnyAsync(i =>
                    i.ImageId == imageId &&
                    i.ListingId == listingId);

        if (!imageExists)
        {
            return false;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE dbo.ListingImages
            SET IsPrimary =
                CASE
                    WHEN ImageId = {imageId}
                        THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END
            WHERE ListingId = {listingId}
            """);

        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);
        await _context.SaveChangesAsync();

        return true;
    }

    // ============================================================
    // BUILD PAGED RESPONSE
    // ============================================================

    private async Task<ListingPagedResponse> BuildPagedResponseAsync(
        List<Listing> listings,
        int page,
        int pageSize,
        int totalItems,
        bool includeReviewHistory = false)
    {
        var listingIds =
            listings.Select(l => l.ListingId)
                .ToList();

        var landlordIds =
            listings
                .Select(l => l.LandlordId)
                .Distinct()
                .ToList();

        var users =
            await _context.Users
                .AsNoTracking()
                .Where(u =>
                    landlordIds.Contains(u.UserId))
                .ToDictionaryAsync(
                    u => u.UserId,
                    u => u.FullName);

        var images =
            await _context.ListingImages
                .AsNoTracking()
                .Where(i =>
                    listingIds.Contains(i.ListingId))
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.ImageId)
                .ToListAsync();

        var imageLookup =
            images
                .GroupBy(i => i.ListingId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(MapImageResponse)
                        .ToList());

        var videos = await _context.ListingVideos.AsNoTracking()
            .Where(v => listingIds.Contains(v.ListingId))
            .Select(v => new { v.ListingId, v.VideoId, v.FileName, v.SizeBytes, v.CreatedAt })
            .ToListAsync();
        var videoLookup = videos.ToDictionary(v => v.ListingId, v => new ListingVideoResponse
        {
            VideoId = v.VideoId,
            VideoUrl = $"/api/Listings/{v.ListingId}/video/content",
            FileName = v.FileName,
            SizeBytes = v.SizeBytes,
            CreatedAt = v.CreatedAt
        });

        var reviewEvents = includeReviewHistory
            ? await (from e in _context.ListingReviewEvents.AsNoTracking()
                     join u in _context.Users.AsNoTracking() on e.ActorUserId equals u.UserId
                     where listingIds.Contains(e.ListingId)
                     orderby e.CreatedAt, e.ReviewEventId
                     select new { e.ListingId, Response = new ListingReviewEventResponse
                     {
                         ReviewEventId = e.ReviewEventId,
                         EventType = e.EventType,
                         Comment = e.Comment,
                         ActorUserId = e.ActorUserId,
                         ActorName = u.FullName,
                         CreatedAt = e.CreatedAt
                     }}).ToListAsync()
            : [];
        var reviewLookup = reviewEvents.GroupBy(e => e.ListingId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Response).ToList());

        var items =
            listings
                .Select(l =>
                {
                    var response =
                        MapListingResponse(
                            l,
                            users.TryGetValue(
                                l.LandlordId,
                                out var name)
                                ? name
                                : "Unknown");

                    response.Images =
                        imageLookup.TryGetValue(
                            l.ListingId,
                            out var listingImages)
                            ? listingImages
                            : [];
                    response.Video = videoLookup.GetValueOrDefault(l.ListingId);
                    if (includeReviewHistory)
                        response.ReviewHistory = reviewLookup.GetValueOrDefault(l.ListingId) ?? [];

                    return response;
                })
                .ToList();

        return new ListingPagedResponse
        {
            Items =
                items,

            Page =
                page,

            PageSize =
                pageSize,

            TotalItems =
                totalItems,

            TotalPages =
                (int)Math.Ceiling(
                    totalItems / (double)pageSize)
        };
    }

    // ============================================================
    // BUILD DETAIL RESPONSE
    // ============================================================

    private async Task<DateTime> NextListingEditTimestampAsync(Listing listing)
    {
        var now = DateTimeExtensions.UtcNowToSecond();
        if (listing.ReviewStatus != ReviewStatuses.ChangesRequested)
            return now;

        var requestedAt = await _context.ListingReviewEvents
            .Where(e => e.ListingId == listing.ListingId && e.EventType == "ChangesRequested")
            .OrderByDescending(e => e.ReviewEventId)
            .Select(e => (DateTime?)e.CreatedAt)
            .FirstOrDefaultAsync();

        // DATETIME2(0) stores whole seconds. Keep an edit made in the same
        // second as the Admin decision ordered after that decision so it can
        // be distinguished from an unchanged resubmission.
        return requestedAt.HasValue && now <= requestedAt.Value
            ? requestedAt.Value.AddSeconds(1)
            : now;
    }

    private async Task<ListingResponse> BuildListingResponseAsync(
        Listing listing,
        bool includeReviewHistory = false)
    {
        var landlordName =
            await _context.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserId == listing.LandlordId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync()
            ?? "Unknown";

        var images =
            await _context.ListingImages
                .AsNoTracking()
                .Where(i =>
                    i.ListingId == listing.ListingId)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.ImageId)
                .Select(i => new ListingImageResponse
                {
                    ImageId =
                        i.ImageId,

                    ImageUrl =
                        "/api/Listings/" + i.ListingId + "/images/" + i.ImageId + "/content",

                    IsPrimary =
                        i.IsPrimary,

                    SortOrder =
                        i.SortOrder
                })
                .ToListAsync();

        var response =
            MapListingResponse(
                listing,
                landlordName);

        response.Images =
            images;

        var video = await _context.ListingVideos.AsNoTracking()
            .Where(v => v.ListingId == listing.ListingId)
            .Select(v => new { v.VideoId, v.FileName, v.SizeBytes, v.CreatedAt })
            .FirstOrDefaultAsync();
        if (video != null)
            response.Video = new ListingVideoResponse
            {
                VideoId = video.VideoId,
                VideoUrl = $"/api/Listings/{listing.ListingId}/video/content",
                FileName = video.FileName,
                SizeBytes = video.SizeBytes,
                CreatedAt = video.CreatedAt
            };

        if (includeReviewHistory)
            response.ReviewHistory = await (from e in _context.ListingReviewEvents.AsNoTracking()
                                            join u in _context.Users.AsNoTracking() on e.ActorUserId equals u.UserId
                                            where e.ListingId == listing.ListingId
                                            orderby e.CreatedAt, e.ReviewEventId
                                            select new ListingReviewEventResponse
                                            {
                                                ReviewEventId = e.ReviewEventId,
                                                EventType = e.EventType,
                                                Comment = e.Comment,
                                                ActorUserId = e.ActorUserId,
                                                ActorName = u.FullName,
                                                CreatedAt = e.CreatedAt
                                            }).ToListAsync();

        return response;
    }

    // ============================================================
    // MAP LISTING RESPONSE
    // ============================================================

    private static ListingResponse MapListingResponse(
        Listing listing,
        string landlordName)
    {
        return new ListingResponse
        {
            ListingId =
                listing.ListingId,

            LandlordId =
                listing.LandlordId,

            LandlordName =
                landlordName,

            Title =
                listing.Title,

            Description =
                listing.Description,

            AddressLine =
                listing.AddressLine,

            Ward =
                listing.Ward,

            District =
                listing.District,

            City =
                listing.City,

            Price =
                listing.Price,

            DepositAmount =
                listing.DepositAmount,

            AreaSqm =
                listing.AreaSqm,

            Bedrooms =
                listing.Bedrooms,

            Bathrooms =
                listing.Bathrooms,

            HasWifi =
                listing.HasWifi,

            HasAirConditioner =
                listing.HasAirConditioner,

            HasParking =
                listing.HasParking,

            HasKitchen =
                listing.HasKitchen,

            HasPrivateBathroom =
                listing.HasPrivateBathroom,

            ElectricityPrice =
                listing.ElectricityPrice,

            WaterPrice =
                listing.WaterPrice,

            ServiceFee =
                listing.ServiceFee,

            Latitude =
                listing.Latitude,

            Longitude =
                listing.Longitude,

            Status =
                listing.Status,

            ReviewStatus =
                listing.ReviewStatus,

            ReviewedByUserId =
                listing.ReviewedByUserId,

            ReviewedAt =
                listing.ReviewedAt,

            RejectReason =
                listing.RejectReason,

            ReviewComment =
                listing.ReviewComment,

            CreatedAt =
                listing.CreatedAt,

            UpdatedAt =
                listing.UpdatedAt
        };
    }

    // ============================================================
    // MAP IMAGE RESPONSE
    // ============================================================

    private static ListingImageResponse MapImageResponse(
        ListingImage image)
    {
        return new ListingImageResponse
        {
            ImageId =
                image.ImageId,

            ImageUrl =
                $"/api/Listings/{image.ListingId}/images/{image.ImageId}/content",

            IsPrimary =
                image.IsPrimary,

            SortOrder =
                image.SortOrder
        };
    }

    // ============================================================
    // VALIDATE LISTING VALUES
    // ============================================================

    private static void ValidateListingValues(
        string title,
        string? description,
        string addressLine,
        string? ward,
        string district,
        string city,
        decimal price,
        decimal depositAmount,
        decimal areaSqm,
        byte bedrooms,
        byte bathrooms,
        decimal? electricityPrice,
        decimal? waterPrice,
        decimal? serviceFee,
        decimal? latitude,
        decimal? longitude)
    {
        if (string.IsNullOrWhiteSpace(title) ||
            title.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Title must contain at least 5 characters.");
        }

        if (string.IsNullOrWhiteSpace(addressLine) ||
            addressLine.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Address must contain at least 5 characters.");
        }

        if (string.IsNullOrWhiteSpace(district) ||
            district.Trim().Length < 2)
        {
            throw new ArgumentException(
                "District is required.");
        }

        if (string.IsNullOrWhiteSpace(city) ||
            city.Trim().Length < 2)
        {
            throw new ArgumentException(
                "City is required.");
        }

        if (!string.IsNullOrWhiteSpace(ward) &&
            ward.Trim().Length < 2)
        {
            throw new ArgumentException(
                "Ward must contain at least 2 characters.");
        }

        if (price <= 0)
        {
            throw new ArgumentException(
                "Price must be greater than 0.");
        }

        if (depositAmount <= 0)
        {
            throw new ArgumentException(
                "Deposit amount must be greater than 0.");
        }

        if (areaSqm <= 0)
        {
            throw new ArgumentException(
                "Area must be greater than 0.");
        }

        if (bedrooms > 20)
        {
            throw new ArgumentException(
                "Bedrooms must be between 0 and 20.");
        }

        if (bathrooms < 1 ||
            bathrooms > 20)
        {
            throw new ArgumentException(
                "Bathrooms must be between 1 and 20.");
        }

        ValidateNonNegative(
            electricityPrice,
            "Electricity price");

        ValidateNonNegative(
            waterPrice,
            "Water price");

        ValidateNonNegative(
            serviceFee,
            "Service fee");

        if (latitude.HasValue &&
            (latitude.Value < -90 ||
             latitude.Value > 90))
        {
            throw new ArgumentException(
                "Latitude must be between -90 and 90.");
        }

        if (longitude.HasValue &&
            (longitude.Value < -180 ||
             longitude.Value > 180))
        {
            throw new ArgumentException(
                "Longitude must be between -180 and 180.");
        }

        var hasLatitude =
            latitude.HasValue;

        var hasLongitude =
            longitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            throw new ArgumentException(
                "Latitude and Longitude must be provided together.");
        }
    }

    // ============================================================
    // VALIDATE PUBLISH REQUIREMENTS
    // ============================================================

    private static void ValidatePublishRequirements(
        string? description,
        int imageCount,
        int primaryImageCount)
    {
        if (string.IsNullOrWhiteSpace(description) ||
            description.Trim().Length < 10)
        {
            throw new InvalidOperationException(
                "An Active listing requires a description of at least 10 characters.");
        }

        if (imageCount <= 0)
        {
            throw new InvalidOperationException(
                "An Active listing requires at least one image.");
        }

        if (primaryImageCount != 1)
        {
            throw new InvalidOperationException(
                "An Active listing requires exactly one primary image.");
        }
    }

    // ============================================================
    // VALIDATE NON-NEGATIVE DECIMAL
    // ============================================================

    private static void ValidateNonNegative(
        decimal? value,
        string fieldName)
    {
        if (value.HasValue &&
            value.Value < 0)
        {
            throw new ArgumentException(
                $"{fieldName} cannot be negative.");
        }
    }

    // ============================================================
    // NORMALIZE OPTIONAL STRING
    // ============================================================

    private static string? NormalizeNullable(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
