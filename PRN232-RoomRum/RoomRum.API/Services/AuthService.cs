using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Auth;
using RoomRum.API.Models;
using RoomRum.API.Settings;

namespace RoomRum.API.Services;

public class AuthService : IAuthService
{
    private readonly RoomRumDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        RoomRumDbContext context,
        JwtSettings jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        string? ipAddress)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim();
        var role = request.Role.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.");
        }

        if (request.Password.Length < 6)
        {
            throw new ArgumentException(
                "Password must contain at least 6 characters.");
        }

        if (!string.Equals(
                role,
                "Tenant",
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                role,
                "Landlord",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only Tenant or Landlord accounts can be registered.");
        }

        role = string.Equals(
            role,
            "Tenant",
            StringComparison.OrdinalIgnoreCase)
            ? "Tenant"
            : "Landlord";

        var normalizedEmail = email.ToUpperInvariant();

        var existingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.EmailNormalized == normalizedEmail);

        if (existingUser != null)
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            request.Password);

        var user = new User
        {
            FullName = fullName,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(request.Phone)
                ? null
                : request.Phone.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,

            // Explicit whole-second CreatedAt keeps CK_Users_UpdatedAt
            // satisfiable when the profile is edited in the same second.
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        return await GenerateAuthResponseAsync(user, ipAddress);
    }

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress)
    {
        var email = request.Email.Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedEmail = email.ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.EmailNormalized == normalizedEmail);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            return null;
        }

        return await GenerateAuthResponseAsync(user, ipAddress);
    }

    public async Task<AuthResponse?> RefreshAsync(
        RefreshRequest request,
        string? ipAddress)
    {
        var rawToken = request.RefreshToken?.Trim();

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = HashToken(rawToken);

        var storedToken = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (storedToken == null)
        {
            return null;
        }

        if (storedToken.RevokedAt != null)
        {
            // Reuse detection (v5.2): a token that was already rotated or
            // logged out came back. Treat the whole session family as leaked
            // and revoke every active token this user still holds.
            var revokedAt = DateTimeExtensions.UtcNowToSecond();

            var activeTokens = await _context.RefreshTokens
                .Where(t =>
                    t.UserId == storedToken.UserId &&
                    t.RevokedAt == null)
                .ToListAsync();

            foreach (var activeToken in activeTokens)
            {
                activeToken.RevokedAt = revokedAt;
                activeToken.RevokedByIp = ipAddress;
            }

            await _context.SaveChangesAsync();

            return null;
        }

        if (storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        if (!storedToken.User.IsActive)
        {
            return null;
        }

        // Rotation: the presented token is revoked and linked to its
        // replacement in the same save, so a crash in between cannot leave two
        // usable tokens behind.
        return await GenerateAuthResponseAsync(
            storedToken.User,
            ipAddress,
            storedToken);
    }

    public async Task<bool> LogoutAsync(
        LogoutRequest request,
        string? ipAddress)
    {
        var rawToken = request.RefreshToken?.Trim();

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var tokenHash = HashToken(rawToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (storedToken == null)
        {
            return false;
        }

        // Logging out twice is fine; the row stays revoked.
        if (storedToken.RevokedAt == null)
        {
            storedToken.RevokedAt = DateTimeExtensions.UtcNowToSecond();
            storedToken.RevokedByIp = ipAddress;

            await _context.SaveChangesAsync();
        }

        return true;
    }

    // ============================================================
    // TOKEN GENERATION
    // ============================================================

    private async Task<AuthResponse> GenerateAuthResponseAsync(
        User user,
        string? ipAddress,
        RefreshToken? rotatedFrom = null)
    {
        var issuedAtUtc = DateTime.UtcNow;

        var expiresAtUtc = issuedAtUtc.AddMinutes(
            _jwtSettings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()),

            new(
                ClaimTypes.Name,
                user.FullName),

            new(
                ClaimTypes.Email,
                user.Email),

            new(
                ClaimTypes.Role,
                user.Role),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            // Issued-at. Without it a client cannot tell how old a token is,
            // which matters for "log out stale session" checks after a password
            // change or a role change.
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAtUtc).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler()
            .WriteToken(token);

        // ------------------------------------------------------------
        // Refresh token (v5.2)
        // ------------------------------------------------------------
        // The raw value goes to the client exactly once. Only the SHA-256 hash
        // is persisted, so a leaked database cannot be replayed.
        var rawRefreshToken = CreateRefreshTokenValue(out var refreshTokenHash);

        var createdAt = DateTimeExtensions.UtcNowCreatedAt();

        var refreshTokenExpiresAtUtc = createdAt.AddDays(
            _jwtSettings.RefreshTokenDays);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            CreatedAt = createdAt,
            ExpiresAt = refreshTokenExpiresAtUtc,
            CreatedByIp = ipAddress
        });

        if (rotatedFrom != null)
        {
            rotatedFrom.RevokedAt = DateTimeExtensions.UtcNowToSecond();
            rotatedFrom.RevokedByIp = ipAddress;
            rotatedFrom.ReplacedByTokenHash = refreshTokenHash;
        }

        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            User = new UserResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role
            }
        };
    }

    /// <summary>
    /// 256-bit opaque token, URL-safe, without padding. 43 characters.
    /// </summary>
    private static string CreateRefreshTokenValue(out string tokenHash)
    {
        var rawToken = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        tokenHash = HashToken(rawToken);

        return rawToken;
    }

    /// <summary>Uppercase SHA-256 hex, matching CK_RefreshTokens_TokenHash.</summary>
    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}