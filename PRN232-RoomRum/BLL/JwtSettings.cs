namespace RoomRum.BLL.Settings;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>
    /// Lifetime of a refresh token (v5.2). Access tokens stay short-lived;
    /// the refresh token is what keeps the user signed in, rotating on every
    /// use and revocable server-side.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 14;
}