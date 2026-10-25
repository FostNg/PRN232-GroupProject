using RoomRum.API.DTOs.Favorites;

namespace RoomRum.API.Services;

public interface IFavoriteService
{
    Task<IReadOnlyList<FavoriteResponseDto>> GetMyFavoritesAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<FavoriteResponseDto> AddFavoriteAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveFavoriteAsync(
        int tenantId,
        int listingId,
        CancellationToken cancellationToken = default);
}