using RoomRum.DAL;
using RoomRum.BLL.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Mail;


using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoomRum.DAL.Models;
using RoomRum.BLL.Settings;

namespace RoomRum.BLL;
public interface IAuthService
{
    Task<ApiOutcome> Register(RegisterRequest request);
    Task<ApiOutcome> Login(LoginRequest request);
    Task<ApiOutcome> Me(Guid userId);
}
public sealed class AuthService(NewRoomRumDbContext db, JwtSettings jwt) : IAuthService
{
    public async Task<ApiOutcome> Register(RegisterRequest request)
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
        var role = await db.Roles.SingleOrDefaultAsync(x => x.RoleName == roleName);
        if (role is null) return ApiOutcome.Unavailable("Role data has not been initialized.");
        var user = new User
        {
            UserId = Guid.NewGuid(), RoleId = role.RoleId, Name = name, Email = email,
            Phone = phone, Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = "Active"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ApiOutcome.Created(new AuthSessionResponse(user.UserId, user.Name, user.Email,
            role.RoleName, Token(user, role.RoleName)));
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
