namespace RoomRum.BLL;

public interface IRegistrationEmailSender
{
    bool IsConfigured { get; }
    string DeliveryMethod { get; }
    Task<bool> SendAsync(string email, string name, string verificationLink, int tokenMinutes);
}

public sealed class EmailVerificationSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int TokenMinutes { get; set; } = 60;
}
