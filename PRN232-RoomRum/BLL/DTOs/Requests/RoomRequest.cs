using System.Text.Json.Serialization;

namespace RoomRum.BLL.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class RoomRequest
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Address { get; set; }
    public decimal? Area { get; set; }
    public decimal? ElectricityFee { get; set; }
    public decimal? WaterFee { get; set; }
    public decimal? ServiceFee { get; set; }
    public decimal? Deposit { get; set; }
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public string? Rule { get; set; }
    public List<Guid>? AmenityIds { get; set; }
    public List<string>? ImageUrls { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool ClearCoordinates { get; set; }
    public bool ReplaceImageUrls { get; set; }
}
