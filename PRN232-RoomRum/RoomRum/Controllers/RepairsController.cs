using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Authorize]
[Route("api/repairs")]
public sealed class RepairsController(IRepairService repairs) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<RepairSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await repairs.Mine(UserId, page, pageSize));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RepairCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(RepairRequest request) => Respond(await repairs.Create(UserId, request));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RepairStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Respond(Guid id, RepairResponseRequest request) =>
        base.Respond(await repairs.Respond(UserId, id, request));
}
