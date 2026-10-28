namespace RoomRum.BLL.DTOs.Requests;

public class ReportRequest
{
    public Guid RoomId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
