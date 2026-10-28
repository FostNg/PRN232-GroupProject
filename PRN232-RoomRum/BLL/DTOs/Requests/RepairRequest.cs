namespace RoomRum.BLL.DTOs.Requests;

public class RepairRequest
{
    public Guid RoomId { get; set; }
    public string? Priority { get; set; }
    public string? RepairItems { get; set; }
    public string? Description { get; set; }
    public DateOnly? ExpectingDate { get; set; }
}
