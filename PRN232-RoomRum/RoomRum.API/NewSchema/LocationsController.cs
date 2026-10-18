using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.BLL;

namespace RoomRum.API.NewSchema;

[Route("api/locations")]
public sealed class LocationsController(ILocationSearchService locations) : ApiControllerBase
{
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LocationSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken) =>
        Respond(await locations.Search(q, cancellationToken));
}
