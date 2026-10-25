namespace RoomRum.BLL.DTOs.Requests;

public sealed class RoomSearchRequest
{
    public string? Q { get; set; }
    public string? Type { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinArea { get; set; }
    public decimal? MaxArea { get; set; }
    public decimal? MaxDeposit { get; set; }
    public decimal? MaxMoveInCost { get; set; }
    public decimal? MaxMonthlyCost { get; set; }
    public int Occupants { get; set; } = 1;
    public int MonthlyKwh { get; set; } = 100;
    public Guid? AmenityId { get; set; }
    public List<Guid>? AmenityIds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? MaxDistanceKm { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
