using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;

namespace RoomRum.API.Controllers;

/// <summary>
/// Shared plumbing for every controller: reads the caller's identity from the
/// JWT once and wraps payloads in the uniform ApiResponse envelope.
///
/// AuthController, ListingsController and FavoritesController predate this base
/// class and keep their existing (already released) response shapes; new
/// modules use these helpers.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Numeric user id from the token. Returns false when the token is missing
    /// or malformed, so the caller can answer 401 instead of throwing.
    /// </summary>
    protected bool TryGetUserId(out int userId)
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return int.TryParse(raw, out userId) && userId > 0;
    }

    /// <summary>Role claim written by AuthService (Tenant / Landlord / Admin).</summary>
    protected string GetRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    protected string GetFullName()
    {
        return User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    }

    protected IActionResult OkEnvelope<T>(T data, string? message = null)
    {
        return Ok(ApiResponse.Ok(data, message));
    }

    protected IActionResult CreatedEnvelope<T>(
        T data,
        string? message = null)
    {
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Ok(data, message));
    }

    protected IActionResult UnauthorizedEnvelope(string message)
    {
        return Unauthorized(ApiResponse.Fail(message));
    }

    /// <summary>
    /// Standard 404 payload. Every error response carries the same three keys
    /// (success, message, data) so the Flutter client decodes one model and
    /// never has to special-case a missing field.
    /// </summary>
    protected IActionResult NotFoundEnvelope(string message)
    {
        return NotFound(ApiResponse.Fail(message));
    }

    protected IActionResult BadRequestEnvelope(string message)
    {
        return BadRequest(ApiResponse.Fail(message));
    }

    protected IActionResult ConflictEnvelope(string message)
    {
        return Conflict(ApiResponse.Fail(message));
    }

    /// <summary>
    /// Standard 401 payload used when the token has no usable user id.
    /// </summary>
    protected IActionResult MissingIdentity()
    {
        return UnauthorizedEnvelope(
            "User identity is missing or invalid in the access token.");
    }
}
