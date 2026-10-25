using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Auth;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        try
        {
            var response =
                await _authService.RegisterAsync(request, GetRemoteIp());

            return CreatedEnvelope(response, "Registration successful.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var response =
            await _authService.LoginAsync(request, GetRemoteIp());

        if (response == null)
        {
            return UnauthorizedEnvelope("Invalid email or password.");
        }

        return OkEnvelope(response, "Login successful.");
    }

    // ============================================================
    // REFRESH (v5.2)
    // POST: /api/Auth/refresh
    // ============================================================
    //
    // Exchanges a refresh token for a new access + refresh pair. The old
    // refresh token is revoked as part of the same rotation. Reusing an
    // already rotated token revokes the whole session family (theft signal).

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request)
    {
        var response =
            await _authService.RefreshAsync(request, GetRemoteIp());

        if (response == null)
        {
            return UnauthorizedEnvelope(
                "Refresh token is invalid, expired or already used.");
        }

        return OkEnvelope(response, "Token refreshed.");
    }

    // ============================================================
    // LOGOUT (v5.2)
    // POST: /api/Auth/logout
    // ============================================================
    //
    // Revokes the presented refresh token. No access token is required: a
    // client whose JWT already expired must still be able to end its session.
    // Logging out twice stays successful.

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request)
    {
        var revoked =
            await _authService.LogoutAsync(request, GetRemoteIp());

        if (!revoked)
        {
            return UnauthorizedEnvelope("Refresh token is invalid.");
        }

        return OkEnvelope<object?>(null, "Logged out.");
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedEnvelope("Invalid authentication token.");
        }

        // Read straight from the token: this endpoint exists so the client can
        // rehydrate its session without another database round trip.
        return OkEnvelope(new
        {
            userId,
            fullName = GetFullName(),
            email = User.FindFirstValue(ClaimTypes.Email),
            role = GetRole()
        });
    }

    /// <summary>
    /// Client IP for the RefreshTokens audit columns. Null when the connection
    /// is in-process (integration tests) instead of over TCP.
    /// </summary>
    private string? GetRemoteIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}