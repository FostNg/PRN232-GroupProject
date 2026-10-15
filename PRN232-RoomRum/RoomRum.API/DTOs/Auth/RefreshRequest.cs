using System.ComponentModel.DataAnnotations;

namespace RoomRum.API.DTOs.Auth;

/// <summary>
/// v5.2 rotating refresh token. The raw value is only ever returned by
/// register/login/refresh; the server stores its SHA-256 hash.
/// </summary>
public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
