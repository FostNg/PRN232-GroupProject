namespace RoomRum.BLL.DTOs.Responses;

public class RoomTenantPeriodResponse
{
    public Guid? HistoryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime HiredAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public bool IsCurrent { get; set; }
}
