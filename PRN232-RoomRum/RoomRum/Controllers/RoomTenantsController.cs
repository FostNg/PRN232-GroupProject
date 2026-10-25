using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Authorize]
[Route("api/room-tenants")]
public sealed class RoomTenantsController(IRoomTenantService tenants) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<RoomTenantSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await tenants.Mine(UserId, page, pageSize));

    [HttpPost("{roomId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Add(Guid roomId, TenantRequest request) =>
        Respond(await tenants.Add(UserId, roomId, request));

    [HttpPost("{roomId:guid}/invite-by-email")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddByEmail(Guid roomId, TenantInviteByEmailRequest request) =>
        Respond(await tenants.AddByEmail(UserId, roomId, request));

    [HttpPatch("{roomId:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantCreatedResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid roomId) =>
        Respond(await tenants.Accept(UserId, roomId));

    [HttpPatch("{roomId:guid}/{tenantId:guid}/end")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantEndedResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> End(Guid roomId, Guid tenantId) =>
        Respond(await tenants.End(UserId, roomId, tenantId));

    [HttpGet("{roomId:guid}/{tenantId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantHistoryPageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid roomId, Guid tenantId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Respond(await tenants.History(UserId, roomId, tenantId, page, pageSize));
}
