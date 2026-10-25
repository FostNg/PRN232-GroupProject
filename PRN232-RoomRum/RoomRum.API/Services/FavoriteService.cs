using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Favorites;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

public sealed class FavoriteService : IFavoriteService
{
    private readonly RoomRumDbContext _context;

    public FavoriteService(RoomRumDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FavoriteResponseDto>> GetMyFavoritesAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var favorites = await
            (
                from favorite in _context.Favorites.AsNoTracking()
                join listing in _context.Listings.AsNoTracking()
                    on favorite.ListingId equals listing.ListingId
                where favorite.TenantId == tenantId
                orderby favorite.CreatedAt descending
                select new FavoriteResponseDto
                {
                    ListingId = listing.ListingId,
                    Title = listing.Title,
                    Price = listing.Price,
                    AreaSqm = listing.AreaSqm,
                    AddressLine = listing.AddressLine,
                    Ward = listing.Ward,
                    District = listing.District,
                    City = listing.City,
                    Status = listing.Status,
                    IsAvailable = listing.Status == ListingStatuses.Active,

                    PrimaryImageUrl = _context.ListingImages
                        .AsNoTracking()
                        .Where(image =>
                            image.ListingId == listing.ListingId &&
                            image.IsPrimary)
                        .Select(image => "/api/Listings/" + image.ListingId + "/images/" + image.ImageId + "/content")
                        .FirstOrDefault(),

                    FavoritedAt = favorite.CreatedAt
                }
            )
            .ToListAsync(cancellationToken);

        return favorites;
    }

    public async Task<FavoriteResponseDto> AddFavoriteAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await _context.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ListingId == listingId,
                cancellationToken);

        if (listing is null)
        {
            throw new KeyNotFoundException("Listing does not exist.");
        }

        // Mirrors dbo.TR_Favorites_Integrity: a Tenant may favourite an Active
        // OR Reserved listing. Reserved rooms are still worth saving - the
        // tenant may want to watch whether the booking falls through - and the
        // database deliberately allows it. Rejecting Reserved here would make
        // the API stricter than the schema and would break the favourites list
        // for anyone who saved a room before it was reserved.
        if (listing.Status != ListingStatuses.Active &&
            listing.Status != ListingStatuses.Reserved)
        {
            throw new InvalidOperationException(
                "Only an Active or Reserved listing can be added to favorites.");
        }

        var existingFavorite = await _context.Favorites
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.TenantId == tenantId &&
                    x.ListingId == listingId,
                cancellationToken);

        if (existingFavorite)
        {
            throw new InvalidOperationException(
                "This listing is already in your favorites.");
        }

        var favorite = new Favorite
        {
            TenantId = tenantId,
            ListingId = listingId,

            // Whole-second UTC, matching every other API-written timestamp.
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        _context.Favorites.Add(favorite);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The composite primary key in SQL Server is the final
            // protection against duplicate favorites in concurrent requests.
            if (IsDuplicateKeyException(exception))
            {
                throw new InvalidOperationException(
                    "This listing is already in your favorites.",
                    exception);
            }

            throw;
        }

        return await GetFavoriteAsync(
            tenantId,
            listingId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The favorite was created but could not be loaded.");
    }

    public async Task<bool> RemoveFavoriteAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(
                x =>
                    x.TenantId == tenantId &&
                    x.ListingId == listingId,
                cancellationToken);

        if (favorite is null)
        {
            return false;
        }

        _context.Favorites.Remove(favorite);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<FavoriteResponseDto?> GetFavoriteAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken)
    {
        var result = await
            (
                from favorite in _context.Favorites.AsNoTracking()
                join listing in _context.Listings.AsNoTracking()
                    on favorite.ListingId equals listing.ListingId
                where
                    favorite.TenantId == tenantId &&
                    favorite.ListingId == listingId
                select new FavoriteResponseDto
                {
                    ListingId = listing.ListingId,
                    Title = listing.Title,
                    Price = listing.Price,
                    AreaSqm = listing.AreaSqm,
                    AddressLine = listing.AddressLine,
                    Ward = listing.Ward,
                    District = listing.District,
                    City = listing.City,
                    Status = listing.Status,
                    IsAvailable = listing.Status == ListingStatuses.Active,

                    PrimaryImageUrl = _context.ListingImages
                        .AsNoTracking()
                        .Where(image =>
                            image.ListingId == listing.ListingId &&
                            image.IsPrimary)
                        .Select(image => "/api/Listings/" + image.ListingId + "/images/" + image.ImageId + "/content")
                        .FirstOrDefault(),

                    FavoritedAt = favorite.CreatedAt
                }
            )
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    private static bool IsDuplicateKeyException(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;

        return sqlException is not null &&
               (sqlException.Number == 2601 ||
                sqlException.Number == 2627);
    }
}