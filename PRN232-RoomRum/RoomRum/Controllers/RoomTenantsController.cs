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
    [ProducesResponseType(typeof(ApiResponse<List<RoomTenantResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await tenants.Mine(UserId, page, pageSize));

    [HttpPost("{roomId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Add(Guid roomId, TenantRequest request) =>
        RespondWrite(await tenants.Add(UserId, roomId, request), "Đã gửi lời mời thuê phòng.");

    [HttpPost("{roomId:guid}/invite-by-email")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddByEmail(Guid roomId, TenantInviteByEmailRequest request) =>
        RespondWrite(await tenants.AddByEmail(UserId, roomId, request), "Đã gửi lời mời thuê phòng.");

    [HttpPatch("{roomId:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid roomId) =>
        RespondWrite(await tenants.Accept(UserId, roomId), "Đã chấp nhận lời mời thuê phòng.");

    [HttpPatch("{roomId:guid}/{tenantId:guid}/end")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> End(Guid roomId, Guid tenantId) =>
        RespondWrite(await tenants.End(UserId, roomId, tenantId), "Đã kết thúc hoặc hủy lời mời thuê phòng.");

    [HttpGet("{roomId:guid}/{tenantId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<RoomTenantHistoryPageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid roomId, Guid tenantId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Respond(await tenants.History(UserId, roomId, tenantId, page, pageSize));
}
