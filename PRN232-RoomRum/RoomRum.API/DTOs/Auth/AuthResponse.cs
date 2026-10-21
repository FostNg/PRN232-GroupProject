namespace RoomRum.API.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// v5.2 rotating refresh token. Store it in secure storage; exchange it at
    /// /api/Auth/refresh before ExpiresAtUtc passes. The value is only sent
    /// once - a refresh response carries a brand-new token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    public UserResponse User { get; set; } = new();
}