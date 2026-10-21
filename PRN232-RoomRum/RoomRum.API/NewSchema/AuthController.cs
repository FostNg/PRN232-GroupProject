using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request) => Respond(await auth.Register(request));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request) => Respond(await auth.Login(request));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me() => Respond(await auth.Me(UserId));
}
