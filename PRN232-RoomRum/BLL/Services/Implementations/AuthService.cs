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
using Microsoft.IdentityModel.Tokens;

namespace RoomRum.BLL.Services.Implementations;

public sealed class AuthService(IUserRepository users, JwtSettings jwt) : IAuthService
{
    public async Task<ApiOutcome> Register(RegisterRequest request)
    {
        var roleName = request.Role?.Trim();
        if (roleName is not ("Tenant" or "Landlord")) return ApiOutcome.BadRequest("Only Tenant or Landlord can register.");
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
            return ApiOutcome.BadRequest("Valid name, email and password of at least 8 characters are required.");
        if (await users.EmailExistsAsync(email)) return ApiOutcome.Conflict("Email already exists.");
        var role = await users.GetRoleAsync(roleName);
        if (role is null) return ApiOutcome.Unavailable("Role data has not been initialized.");
        var user = new User
        {
            UserId = Guid.NewGuid(), RoleId = role.RoleId, Name = name, Email = email,
            Phone = phone, Dob = request.Dob, Gender = gender, ImageUrl = imageUrl,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = "Inactive"
        };
        users.Add(user);
        await users.SaveChangesAsync();
        return ApiOutcome.Created(new RegistrationResponse(user.UserId, user.Name, user.Email,
            role.RoleName, user.Status));
    }
    public async Task<ApiOutcome> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiOutcome.NotFound("Account not found.");
        var user = await users.GetByEmailWithRoleAsync(request.Email.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return ApiOutcome.NotFound("Account not found.");
        if (user.Status != "Active") return ApiOutcome.Unauthorized("Account is inactive.");
        return ApiOutcome.Ok(new AuthSessionResponse(user.UserId, user.Name, user.Email,
            user.Role.RoleName, Token(user, user.Role.RoleName)));
    }
    public async Task<ApiOutcome> Me(Guid userId)
    {
        var user = await users.GetWithRoleAsync(userId);
        return user is null ? ApiOutcome.NotFound() : ApiOutcome.Ok(new CurrentUserResponse(
            user.UserId, user.Name, user.Email, user.Phone, user.Dob, user.Gender,
            user.ImageUrl, user.Status, user.Role.RoleName));
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
