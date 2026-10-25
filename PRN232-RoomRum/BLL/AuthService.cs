using RoomRum.DAL;
using RoomRum.BLL.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Mail;
using System.Security.Cryptography;


using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoomRum.DAL.Models;
using RoomRum.BLL.Settings;

namespace RoomRum.BLL;
public interface IAuthService
{
    Task<ApiOutcome> Register(RegisterRequest request, IReadOnlyList<RegistrationFile>? files = null);
    Task<ApiOutcome> VerifyEmail(Guid userId, string? token);
    Task<ApiOutcome> SendVerificationEmail(LoginRequest request);
    Task<ApiOutcome> Login(LoginRequest request);
    Task<ApiOutcome> Me(Guid userId);
}
public sealed class AuthService(NewRoomRumDbContext db, JwtSettings jwt,
    IRegistrationEmailSender emailSender, EmailVerificationSettings verification) : IAuthService
{
    public async Task<ApiOutcome> Register(RegisterRequest request, IReadOnlyList<RegistrationFile>? files = null)
    {
        var roleName = request.Role?.Trim();
        if (roleName is not ("Tenant" or "Landlord")) return ApiOutcome.BadRequest("Only Tenant or Landlord can register.");
        var name = request.Name?.Trim();
        var email = request.Email?.Trim();
        var phone = request.Phone?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8
            || name.Length > 255 || email.Length > 255 || email.Any(c => c > 127)
            || !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email
            || phone?.Length > 20 || phone?.Any(c => c > 127) == true)
            return ApiOutcome.BadRequest("Valid name, email and password of at least 8 characters are required.");
        if (await db.Users.AnyAsync(x => x.Email == email)) return ApiOutcome.Conflict("Email already exists.");
        files ??= [];
        if (roleName == "Landlord" &&
            (files.Count(x => x.Category == "PropertyImage") is < 1 or > 3 ||
             files.Count(x => x.Category == "IdentityDocument") is < 1 or > 2 || files.Count > 5))
            return ApiOutcome.BadRequest("Landlords must submit 1-3 property images and 1-2 verification documents using multipart registration.");
        if (roleName == "Tenant" && files.Count > 0)
            return ApiOutcome.BadRequest("Tenant registration does not accept verification files.");
        if (roleName == "Tenant" && !EmailVerificationIsConfigured())
            return ApiOutcome.Unavailable("Email verification is not configured. Contact the administrator; no account was created.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.RoleName == roleName);
        if (role is null) return ApiOutcome.Unavailable("Role data has not been initialized.");
        var user = new User
        {
            UserId = Guid.NewGuid(), RoleId = role.RoleId, Name = name, Email = email,
            Phone = phone, Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
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
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.Add(user);
        foreach (var file in files)
            db.UserRegistrationFiles.Add(new UserRegistrationFile
            {
                FileId = Guid.NewGuid(), UserId = user.UserId, Category = file.Category,
                FileName = file.FileName, ContentType = file.ContentType, Content = file.Content,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        if (verificationLink is not null && !await emailSender.SendAsync(email, name, verificationLink, verification.TokenMinutes))
            return ApiOutcome.Unavailable("Verification email could not be sent. Please retry later; no account was created.");
        await transaction.CommitAsync();
        return ApiOutcome.Created(new RegistrationResponse(user.UserId, user.Name, user.Email,
            role.RoleName, user.Status, verificationLink is null ? null : emailSender.DeliveryMethod));
    }

    public async Task<ApiOutcome> SendVerificationEmail(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiOutcome.Unauthorized("Email hoặc mật khẩu không đúng.");
        var user = await db.Users.Include(x => x.Role).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email == request.Email.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return ApiOutcome.Unauthorized("Email hoặc mật khẩu không đúng.");
        if (user.Role.RoleName != "Tenant")
            return ApiOutcome.BadRequest("Xác thực email dành cho tài khoản Khách thuê.");
        if (user.Status == "Active")
            return ApiOutcome.Ok(new VerificationEmailResponse("Tài khoản đã kích hoạt. Bạn có thể đăng nhập.", null));
        // Do not use email verification to unlock a previously verified account disabled by an admin.
        if (user.Status != "Inactive" || user.EmailVerifiedAt is not null)
            return ApiOutcome.Forbidden();
        if (!EmailVerificationIsConfigured())
            return ApiOutcome.Unavailable("Máy chủ chưa cấu hình gửi email. Vui lòng liên hệ quản trị viên.");

        var now = DateTime.UtcNow;
        var cooldown = now.AddMinutes(-1);
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The SQL condition serializes concurrent requests and enforces a persisted per-account cooldown.
        var updated = await db.Users.Where(x => x.UserId == user.UserId && x.RoleId == user.RoleId &&
                x.Status == "Inactive" && x.EmailVerifiedAt == null &&
                (x.EmailVerificationSentAt == null || x.EmailVerificationSentAt <= cooldown))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.EmailVerificationTokenHash, tokenHash)
                .SetProperty(x => x.EmailVerificationExpiresAt, now.AddMinutes(verification.TokenMinutes))
                .SetProperty(x => x.EmailVerificationSentAt, now));
        if (updated == 0)
            return new ApiOutcome(429, Message: "Vui lòng chờ 60 giây trước khi gửi lại email xác thực.");
        var link = $"{verification.ApiBaseUrl.TrimEnd('/')}/api/auth/verify-email?userId={user.UserId}&token={rawToken}";
        if (!await emailSender.SendAsync(user.Email!, user.Name, link, verification.TokenMinutes))
            return ApiOutcome.Unavailable("Chưa gửi được email xác thực. Vui lòng thử lại sau.");
        await transaction.CommitAsync();
        return ApiOutcome.Ok(new VerificationEmailResponse("Đã gửi email xác thực. Vui lòng kiểm tra hộp thư và thư rác.", emailSender.DeliveryMethod));
    }

    private bool EmailVerificationIsConfigured() => emailSender.IsConfigured &&
        Uri.TryCreate(verification.ApiBaseUrl, UriKind.Absolute, out var apiUri) &&
        apiUri.Scheme is "http" or "https" && apiUri.Query.Length == 0 &&
        apiUri.Fragment.Length == 0 && apiUri.UserInfo.Length == 0 &&
        verification.TokenMinutes is >= 5 and <= 1440;

    public async Task<ApiOutcome> VerifyEmail(Guid userId, string? token)
    {
        if (userId == Guid.Empty || token?.Length != 64 || !token.All(Uri.IsHexDigit))
            return ApiOutcome.BadRequest("Invalid or expired verification link.");
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var now = DateTime.UtcNow;
        // One conditional SQL update makes consumption atomic, including concurrent requests.
        var updated = await db.Users.Where(x => x.UserId == userId && x.Status == "Inactive" && x.EmailVerifiedAt == null &&
                x.EmailVerificationTokenHash == tokenHash && x.EmailVerificationExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Active")
                .SetProperty(x => x.EmailVerifiedAt, now)
                .SetProperty(x => x.EmailVerificationTokenHash, (byte[]?)null)
                .SetProperty(x => x.EmailVerificationExpiresAt, (DateTime?)null));
        return updated == 1
            ? ApiOutcome.Ok(new { status = "Active", message = "Email verified. You can now sign in." })
            : ApiOutcome.BadRequest("Invalid or expired verification link.");
    }
    public async Task<ApiOutcome> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiOutcome.Unauthorized("Invalid credentials.");
        var user = await db.Users.Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == request.Email.Trim());
        if (user is null || user.Status != "Active" || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return ApiOutcome.Unauthorized("Invalid credentials.");
        return ApiOutcome.Ok(new AuthSessionResponse(user.UserId, user.Name, user.Email,
            user.Role.RoleName, Token(user, user.Role.RoleName)));
    }
    public async Task<ApiOutcome> Me(Guid userId)
    {
        var user = await db.Users.Include(x => x.Role).AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId);
        return user is null ? ApiOutcome.NotFound() : ApiOutcome.Ok(new CurrentUserResponse(
            user.UserId, user.Name, user.Email, user.Phone, user.Dob, user.Gender,
            user.ImageUrl, user.Status, user.Role.RoleName));
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
