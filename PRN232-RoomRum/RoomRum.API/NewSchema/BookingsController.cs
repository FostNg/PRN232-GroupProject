using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Authorize]
[Route("api/appointments")]
public sealed class AppointmentsController(IAppointmentService appointments) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await appointments.Mine(UserId, page, pageSize));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AppointmentMutationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(AppointmentRequest request) =>
        Respond(await appointments.Create(UserId, request));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentMutationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(Guid id, StatusRequest request) =>
        Respond(await appointments.UpdateStatus(UserId, id, request));
}

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
