using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<RegistrationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request) => Respond(await auth.Register(request));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login(LoginRequest request) => Respond(await auth.Login(request));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me() => Respond(await auth.Me(UserId));
}
