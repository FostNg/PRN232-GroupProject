using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RoomRum.BLL;
using RoomRum.API.Common;
using RoomRum.API.Registration;

namespace RoomRum.API.NewSchema;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<RegistrationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request) => Respond(await auth.Register(request));

    [HttpPost("send-verification-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<VerificationEmailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendVerificationEmail(LoginRequest request) =>
        Respond(await auth.SendVerificationEmail(request));

    [HttpPost("register/landlord")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 26 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RegistrationResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterLandlord([FromForm] LandlordRegistrationForm form)
    {
        if (form.PropertyImages.Count is < 1 or > 3 || form.IdentityDocuments.Count is < 1 or > 2)
            return BadRequest("Choose 1-3 property images and 1-2 verification documents.");
        var files = new List<RegistrationFile>();
        foreach (var (category, file) in form.PropertyImages.Select(x => ("PropertyImage", x))
                     .Concat(form.IdentityDocuments.Select(x => ("IdentityDocument", x))))
        {
            if (file.Length is < 12 or > RegistrationFileValidator.MaxFileBytes)
                return BadRequest("Each file must be non-empty and at most 5 MB.");
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, HttpContext.RequestAborted);
            var validated = RegistrationFileValidator.Validate(category, file.FileName, stream.ToArray());
            if (validated is null)
                return BadRequest("Use JPEG, PNG or WebP images; verification documents also accept PDF. File content must match its extension.");
            files.Add(validated);
        }
        return Respond(await auth.Register(new RegisterRequest(form.Name, form.Email, form.Password, form.Phone, "Landlord"), files));
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromQuery] Guid userId, [FromQuery] string? token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Respond(await auth.VerifyEmail(userId, token));
    }

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
