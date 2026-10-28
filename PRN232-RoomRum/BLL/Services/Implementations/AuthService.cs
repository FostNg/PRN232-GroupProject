using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.BLL.DTOs;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace RoomRum.BLL.Services.Implementations;

public sealed class AuthService(IUserRepository users, JwtSettings jwt,
    IRegistrationEmailSender emailSender, EmailVerificationSettings verification) : IAuthService
{
    public async Task<ApiOutcomeResponse> Register(RegisterRequest request, IReadOnlyList<RegistrationFile>? files = null)
    {
        var roleName = request.Role?.Trim();
        if (roleName is not ("Tenant" or "Landlord")) return ApiOutcomeResponse.BadRequest("Only Tenant or Landlord can register.");
        var name = request.Name?.Trim();
        var email = request.Email?.Trim();
        var phone = request.Phone?.Trim();
        var gender = request.Gender?.Trim();
        var imageUrl = request.ImageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8
            || name.Length > 255 || email.Length > 255 || email.Any(c => c > 127)
            || !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email
            || phone?.Length > 20 || phone?.Any(c => c > 127) == true
            || gender?.Length > 20 || imageUrl?.Any(c => c > 127) == true)
            return ApiOutcomeResponse.BadRequest("Valid name, email and password of at least 8 characters are required.");
        if (await users.EmailExistsAsync(email)) return ApiOutcomeResponse.Conflict("Email already exists.");
        files ??= [];
        if (roleName == "Landlord" &&
            (files.Count(x => x.Category == "PropertyImage") is < 1 or > 3 ||
             files.Count(x => x.Category == "IdentityDocument") is < 1 or > 2 || files.Count > 5))
            return ApiOutcomeResponse.BadRequest("Landlords must submit 1-3 property images and 1-2 verification documents using multipart registration.");
        if (roleName == "Tenant" && files.Count > 0)
            return ApiOutcomeResponse.BadRequest("Tenant registration does not accept verification files.");
        if (roleName == "Tenant" && !EmailVerificationIsConfigured())
            return ApiOutcomeResponse.Unavailable("Email verification is not configured. Contact the administrator; no account was created.");
        var role = await users.GetRoleAsync(roleName);
        if (role is null) return ApiOutcomeResponse.Unavailable("Role data has not been initialized.");
        var user = new User
        {
            UserId = Guid.NewGuid(), RoleId = role.RoleId, Name = name, Email = email,
            Phone = phone, Dob = request.Dob, Gender = gender, ImageUrl = imageUrl,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = roleName == "Tenant" ? "Inactive" : "Active"
        };
        string? verificationLink = null;
        if (roleName == "Tenant")
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.EmailVerificationTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            user.EmailVerificationExpiresAt = DateTime.UtcNow.AddMinutes(verification.TokenMinutes);
            user.EmailVerificationSentAt = DateTime.UtcNow;
            verificationLink = $"{verification.ApiBaseUrl.TrimEnd('/')}/api/auth/verify-email?userId={user.UserId}&token={rawToken}";
        }
        var registrationFiles = new List<UserRegistrationFile>();
        foreach (var file in files)
            registrationFiles.Add(new UserRegistrationFile
            {
                FileId = Guid.NewGuid(), UserId = user.UserId, Category = file.Category,
                FileName = file.FileName, ContentType = file.ContentType, Content = file.Content,
                CreatedAt = DateTime.UtcNow
            });
        var created = await users.CreateRegistrationAsync(user, registrationFiles, verificationLink is null
            ? null : () => emailSender.SendAsync(email, name, verificationLink, verification.TokenMinutes));
        if (!created)
            return ApiOutcomeResponse.Unavailable("Verification email could not be sent. Please retry later; no account was created.");
        return ApiOutcomeResponse.Created(new RegistrationResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = role.RoleName,
            Status = user.Status,
            EmailDelivery = verificationLink is null ? null : emailSender.DeliveryMethod
        });
    }
    public async Task<ApiOutcomeResponse> SendVerificationEmail(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiOutcomeResponse.Unauthorized("Email hoặc mật khẩu không đúng.");
        var user = await users.GetByEmailWithRoleAsync(request.Email.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return ApiOutcomeResponse.Unauthorized("Email hoặc mật khẩu không đúng.");
        if (user.Role.RoleName != "Tenant")
            return ApiOutcomeResponse.BadRequest("Xác thực email dành cho tài khoản Khách thuê.");
        if (user.Status == "Active")
            return ApiOutcomeResponse.Ok(new VerificationEmailResponse("Tài khoản đã kích hoạt. Bạn có thể đăng nhập.", null));
        if (user.Status != "Inactive" || user.EmailVerifiedAt is not null)
            return ApiOutcomeResponse.Forbidden();
        if (!EmailVerificationIsConfigured())
            return ApiOutcomeResponse.Unavailable("Máy chủ chưa cấu hình gửi email. Vui lòng liên hệ quản trị viên.");

        var now = DateTime.UtcNow;
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var link = $"{verification.ApiBaseUrl.TrimEnd('/')}/api/auth/verify-email?userId={user.UserId}&token={rawToken}";
        var sent = await users.ResendVerificationEmailAsync(user.UserId, user.RoleId, tokenHash,
            now.AddMinutes(verification.TokenMinutes), now,
            () => emailSender.SendAsync(user.Email!, user.Name, link, verification.TokenMinutes));
        if (sent == VerificationEmailSendResult.Cooldown)
            return new ApiOutcomeResponse { StatusCode = 429, Message = "Vui lòng chờ 60 giây trước khi gửi lại email xác thực." };
        if (sent == VerificationEmailSendResult.DeliveryFailed)
            return ApiOutcomeResponse.Unavailable("Chưa gửi được email xác thực. Vui lòng thử lại sau.");
        return ApiOutcomeResponse.Ok(new VerificationEmailResponse("Đã gửi email xác thực. Vui lòng kiểm tra hộp thư và thư rác.", emailSender.DeliveryMethod));
    }

    public async Task<ApiOutcomeResponse> VerifyEmail(Guid userId, string? token)
    {
        if (userId == Guid.Empty || token?.Length != 64 || !token.All(Uri.IsHexDigit))
            return ApiOutcomeResponse.BadRequest("Invalid or expired verification link.");
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var now = DateTime.UtcNow;
        var verified = await users.VerifyEmailAsync(userId, tokenHash, now);
        return verified
            ? ApiOutcomeResponse.Ok(new { status = "Active", message = "Email verified. You can now sign in." })
            : ApiOutcomeResponse.BadRequest("Invalid or expired verification link.");
    }

    private bool EmailVerificationIsConfigured() => emailSender.IsConfigured &&
        Uri.TryCreate(verification.ApiBaseUrl, UriKind.Absolute, out var apiUri) &&
        apiUri.Scheme is "http" or "https" && apiUri.Query.Length == 0 &&
        apiUri.Fragment.Length == 0 && apiUri.UserInfo.Length == 0 &&
        verification.TokenMinutes is >= 5 and <= 1440;
    public async Task<ApiOutcomeResponse> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiOutcomeResponse.NotFound("Account not found.");
        var user = await users.GetByEmailWithRoleAsync(request.Email.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return ApiOutcomeResponse.NotFound("Account not found.");
        if (user.Status != "Active") return ApiOutcomeResponse.Unauthorized("Account is inactive.");
        return ApiOutcomeResponse.Ok(new AuthSessionResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.RoleName,
            Token = Token(user, user.Role.RoleName)
        });
    }
    public async Task<ApiOutcomeResponse> Me(Guid userId)
    {
        var user = await users.GetWithRoleAsync(userId);
        return user is null ? ApiOutcomeResponse.NotFound() : ApiOutcomeResponse.Ok(new UserResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Dob = user.Dob,
            Gender = user.Gender,
            ImageUrl = user.ImageUrl,
            Status = user.Status,
            Role = user.Role.RoleName
        });
    }

    public async Task<bool> ValidateIdentity(Guid userId, string? role)
    {
        var user = await users.GetWithRoleAsync(userId);
        return user is not null && user.Status == "Active" && user.Role.RoleName == role;
    }

    private string Token(User user, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, role)
        };
        var signing = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.AccessTokenMinutes), signingCredentials: signing);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
