using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Route("api/rooms")]
public sealed class RoomsController(IRoomService rooms) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RoomSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] RoomSearchRequest request) =>
        Respond(await rooms.Search(request));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RoomDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid id) => Respond(await rooms.Detail(id));

    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<RoomMineResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await rooms.Mine(UserId, page, pageSize));

    [HttpPost]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<RoomMutationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(RoomRequest request) => Respond(await rooms.Create(UserId, request));

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RoomMutationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, RoomRequest request) =>
        Respond(await rooms.Update(UserId, User.IsInRole("Admin"), id, request));

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Hide(Guid id) => Respond(await rooms.Hide(UserId, User.IsInRole("Admin"), id));

    [HttpGet("amenities")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<RoomAmenityResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Amenities([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await rooms.Amenities(page, pageSize));
}
