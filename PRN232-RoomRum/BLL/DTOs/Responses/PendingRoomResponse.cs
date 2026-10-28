namespace RoomRum.BLL.DTOs.Responses;

public class PendingRoomResponse
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
}
