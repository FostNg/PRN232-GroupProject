namespace RoomRum.BLL.DTOs.Requests;

public class SharingRequest
{
    public Guid RoomId { get; set; }
    public string? Title { get; set; }
    public int? Quantity { get; set; }
    public string? Gender { get; set; }
    public decimal? SharingFee { get; set; }
    public string? Introduction { get; set; }
    public string? Rule { get; set; }
}
