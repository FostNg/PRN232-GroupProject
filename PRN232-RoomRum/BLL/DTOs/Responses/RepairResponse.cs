namespace RoomRum.BLL.DTOs.Responses;

public class RepairResponse
{
    public Guid MaintenanceId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public string? Priority { get; set; }
    public string? RepairItems { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateOnly? ExpectingDate { get; set; }
    public string? Response { get; set; }
    public string? RoomTitle { get; set; }
    public string TenantName { get; set; } = string.Empty;
}
