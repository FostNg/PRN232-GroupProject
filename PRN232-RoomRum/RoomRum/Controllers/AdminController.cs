using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;
using RoomRum.DAL.Entities;

namespace RoomRum.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(IAdminService admin) : ApiControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<List<UserResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Users([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await admin.Users(page, pageSize));

    [HttpGet("users/{id:guid}/registration-files")]
    [ProducesResponseType(typeof(ApiResponse<List<RegistrationFileResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegistrationFiles(Guid id)
    {
        Response.Headers.CacheControl = "no-store";
        return Respond(await admin.RegistrationFiles(id));
    }

    [HttpGet("users/{id:guid}/registration-files/{fileId:guid}")]
    public async Task<IActionResult> RegistrationFile(Guid id, Guid fileId)
    {
        var result = await admin.RegistrationFile(id, fileId);
        if (result.StatusCode != 200) return Respond(result);
        var file = (UserRegistrationFile)result.Data!;
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType);
    }

    [HttpPatch("users/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request) =>
        RespondWrite(await admin.SetStatus(UserId, id, request), "Đã cập nhật trạng thái người dùng.");

    [HttpPatch("users/{id:guid}/role")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRole(Guid id, RoleRequest request) =>
        RespondWrite(await admin.SetRole(UserId, id, request), "Đã cập nhật vai trò người dùng.");

    [HttpPost("amenities")]
    [ProducesResponseType(typeof(ApiResponse<RoomAmenityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RoomAmenityResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAmenity(AmenityRequest request) => Respond(await admin.AddAmenity(request));

    [HttpPatch("rooms/{id:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewRoom(Guid id, StatusRequest request) =>
        RespondWrite(await admin.ReviewRoom(id, request), "Đã cập nhật trạng thái phòng.");

    [HttpGet("rooms/pending")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingRoomResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PendingRooms([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await admin.PendingRooms(page, pageSize));
}
