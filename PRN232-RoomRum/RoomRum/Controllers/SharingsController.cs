using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Route("api/sharings")]
public sealed class SharingsController(ISharingService sharings) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SharingSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? roomId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await sharings.List(roomId, page, pageSize));

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SharingMutationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SharingRequest request) => Respond(await sharings.Create(UserId, request));

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id) =>
        Respond(await sharings.Delete(UserId, User.IsInRole("Admin"), id));
}
