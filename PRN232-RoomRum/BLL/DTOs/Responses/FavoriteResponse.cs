namespace RoomRum.BLL.DTOs.Responses;

public class FavoriteResponse
{
    public Guid FavoriteId { get; set; }
    public Guid RoomId { get; set; }
    public string? Title { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
}
