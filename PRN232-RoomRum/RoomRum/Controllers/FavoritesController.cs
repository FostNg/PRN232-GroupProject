using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Authorize]
[Route("api/favorites")]
public sealed class FavoritesController(IFavoriteService favorites) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<FavoriteSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await favorites.Mine(UserId, page, pageSize));

    [HttpPost("{roomId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FavoriteMutationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FavoriteMutationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Add(Guid roomId) => Respond(await favorites.Add(UserId, roomId));

    [HttpDelete("{roomId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid roomId) => Respond(await favorites.Remove(UserId, roomId));
}
