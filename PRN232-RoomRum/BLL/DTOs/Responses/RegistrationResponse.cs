namespace RoomRum.BLL.DTOs.Responses;

public class RegistrationResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EmailDelivery { get; set; }
}
