namespace RoomRum.BLL.DTOs.Responses;

public class UserResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? Dob { get; set; }
    public string? Gender { get; set; }
    public string? ImageUrl { get; set; }
    public string? Status { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
