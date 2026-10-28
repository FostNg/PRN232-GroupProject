namespace RoomRum.BLL.DTOs.Responses;

public class SharingResponse
{
    public Guid SharingId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public string? Title { get; set; }
    public int? Quantity { get; set; }
    public string? Gender { get; set; }
    public decimal? SharingFee { get; set; }
    public string? Introduction { get; set; }
    public string? Rule { get; set; }
    public string? RoomTitle { get; set; }
    public string Author { get; set; } = string.Empty;
}
