namespace RoomRum.BLL.DTOs.Responses;

public class RoomOwnerResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
