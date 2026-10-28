namespace RoomRum.BLL.DTOs.Responses;

public class RoomTenantHistoryPageResponse
{
    public List<RoomTenantPeriodResponse> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
