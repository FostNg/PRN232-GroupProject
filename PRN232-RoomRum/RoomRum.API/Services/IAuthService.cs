using RoomRum.API.DTOs.Auth;

namespace RoomRum.API.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        string? ipAddress);

    Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress);

    /// <summary>
    /// Exchanges a valid refresh token for a new access + refresh token pair
    /// (rotation). Returns null when the token is unknown, expired, revoked or
    /// belongs to an inactive account. Presenting a revoked token is treated
    /// as reuse and revokes every active token of that user.
    /// </summary>
    Task<AuthResponse?> RefreshAsync(RefreshRequest request, string? ipAddress);

    /// <summary>
    /// Revokes one refresh token. Returns false when the token is unknown;
    /// logging out twice is idempotent (returns true).
    /// </summary>
    Task<bool> LogoutAsync(LogoutRequest request, string? ipAddress);
}