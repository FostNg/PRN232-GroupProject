using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Route("api/rooms/{roomId:guid}/ratings")]
public sealed class RatingsController(IRatingService ratings) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<RatingSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid roomId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await ratings.List(roomId, page, pageSize));

    [HttpPut("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RatingMutationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(Guid roomId, RatingRequest request) =>
        Respond(await ratings.Upsert(UserId, roomId, request));
}
