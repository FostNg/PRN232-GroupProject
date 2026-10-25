using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Favorites;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Tenant")]
public sealed class FavoritesController : ApiControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<FavoriteResponseDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyFavorites(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        var favorites = await _favoriteService.GetMyFavoritesAsync(
            tenantId,
            cancellationToken);

        // Wrapped like every other endpoint: a bare JSON array here would force
        // the Flutter client to special-case this one response shape.
        return OkEnvelope(favorites);
    }

    [HttpPost("{listingId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<FavoriteResponseDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddFavorite(
        int listingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("ListingId must be greater than zero.");
        }

        try
        {
            var favorite = await _favoriteService.AddFavoriteAsync(
                tenantId,
                listingId,
                cancellationToken);

            return OkEnvelope(favorite, "Added to your favorites.");
        }
        catch (KeyNotFoundException exception)
        {
            return NotFoundEnvelope(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ConflictEnvelope(exception.Message);
        }
    }

    [HttpDelete("{listingId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(
        int listingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("ListingId must be greater than zero.");
        }

        var removed = await _favoriteService.RemoveFavoriteAsync(
            tenantId,
            listingId,
            cancellationToken);

        if (!removed)
        {
            return NotFoundEnvelope("This listing is not in your favorites.");
        }

        // 204 on purpose: there is nothing meaningful to return, and a body
        // would only add bytes the client has to discard.
        return NoContent();
    }
}