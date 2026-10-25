namespace RoomRum.DAL.Models;

public partial class User
{
    public byte[]? EmailVerificationTokenHash { get; set; }
    public DateTime? EmailVerificationExpiresAt { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
}

public sealed class UserRegistrationFile
{
    public Guid FileId { get; set; }
    public Guid UserId { get; set; }
    public string Category { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
