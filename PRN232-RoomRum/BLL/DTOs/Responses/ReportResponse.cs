namespace RoomRum.BLL.DTOs.Responses;

public class ReportResponse
{
    public Guid ReportId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public string? Reason { get; set; }
    public DateTime? ReportDate { get; set; }
    public string? Status { get; set; }
    public string? RoomTitle { get; set; }
}
