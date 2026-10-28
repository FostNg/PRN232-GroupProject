namespace RoomRum.DAL.Entities;

public sealed class UserRegistrationFile
{
    public Guid FileId { get; set; }
    public Guid UserId { get; set; }
    public string Category { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
