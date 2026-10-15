using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(IAdminService admin) : ApiControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminUserSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Users([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await admin.Users(page, pageSize));

    [HttpPatch("users/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request) =>
        Respond(await admin.SetStatus(UserId, id, request));

    [HttpPatch("users/{id:guid}/role")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserRoleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRole(Guid id, RoleRequest request) =>
        Respond(await admin.SetRole(UserId, id, request));

    [HttpPost("amenities")]
    [ProducesResponseType(typeof(ApiResponse<RoomAmenityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RoomAmenityResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAmenity(AmenityRequest request) => Respond(await admin.AddAmenity(request));
}
