namespace RoomRum.BLL.DTOs.Responses;

public class RatingResponse
{
    public Guid RatingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime RatingDate { get; set; }
    public string User { get; set; } = string.Empty;
}
