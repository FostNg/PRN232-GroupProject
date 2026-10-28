namespace RoomRum.BLL.DTOs.Responses;

public class RoomResponse
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? Address { get; set; }
    public decimal? Area { get; set; }
    public decimal? ElectricityFee { get; set; }
    public decimal? WaterFee { get; set; }
    public decimal? ServiceFee { get; set; }
    public decimal? Deposit { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public string? Rule { get; set; }
    public RoomOwnerResponse? Owner { get; set; }
    public IReadOnlyList<RoomAmenityResponse>? Amenities { get; set; }
    public IReadOnlyList<RoomImageResponse>? Images { get; set; }
    public double? AverageRating { get; set; }
    public int? RatingCount { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public double? DistanceKm { get; set; }
}
