namespace RoomRum.DAL.Entities;

public sealed class RoomTenantHistory
{
    public Guid HistoryId { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public DateTime HiredAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ArchivedAt { get; set; }
    public RoomTenant RoomTenant { get; set; } = null!;
}
