using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Interfaces;

namespace RoomRum.API.Controllers;

[Route("api/locations")]
public sealed class LocationsController(ILocationSearchService locations) : ApiControllerBase
{
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LocationSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken) =>
        Respond(await locations.Search(q, cancellationToken));

    [HttpGet("suggest")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<LocationSearchResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken cancellationToken) =>
        Respond(await locations.Suggest(q, cancellationToken));
}
