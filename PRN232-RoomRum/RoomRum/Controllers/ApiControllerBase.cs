using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API;
using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Interfaces;

namespace RoomRum.API.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid user identity.");

    protected IActionResult OkData<T>(T data) => Ok(ApiResponse.Ok(data));
    protected IActionResult CreatedData<T>(T data) => StatusCode(201, ApiResponse.Ok(data));

    protected IActionResult Respond(ApiOutcome result) => result.StatusCode switch
    {
        200 => OkData(result.Data),
        201 => CreatedData(result.Data),
        204 => NoContent(),
        400 => BadRequest(result.Message),
        401 => Unauthorized(result.Message),
        403 => Forbid(),
        404 => NotFound(result.Message),
        409 => Conflict(result.Message),
        503 => StatusCode(503, result.Message),
        _ => StatusCode(result.StatusCode, result.Message)
    };
}
