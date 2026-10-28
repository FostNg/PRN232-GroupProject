namespace RoomRum.BLL.DTOs.Responses;

public class RoomSearchResponse
{
    public IReadOnlyList<RoomResponse> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
