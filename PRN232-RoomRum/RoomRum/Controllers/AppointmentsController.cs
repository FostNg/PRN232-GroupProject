using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

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
