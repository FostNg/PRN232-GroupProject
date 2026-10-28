using System.Security.Claims;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

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
    [ProducesResponseType(typeof(ApiResponse<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid id)
    {
        Guid? viewerId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var idFromToken)
            ? idFromToken : null;
        return Respond(await rooms.Detail(id, viewerId));
    }

    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<RoomMineResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await rooms.Mine(UserId, page, pageSize));

    [HttpPost]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(RoomRequest request) =>
        RespondCreatedWithData(await rooms.Create(UserId, request), "Đã tạo phòng.",
            data => new { RoomId = (Guid)data! });

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, RoomRequest request) =>
        RespondWrite(await rooms.Update(UserId, User.IsInRole("Admin"), id, request), "Đã cập nhật phòng.");

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
