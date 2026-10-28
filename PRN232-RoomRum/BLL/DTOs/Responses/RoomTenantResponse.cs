namespace RoomRum.BLL.DTOs.Responses;

public class RoomTenantResponse
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime HiredAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public string? RoomTitle { get; set; }
    public string TenantName { get; set; } = string.Empty;
}
