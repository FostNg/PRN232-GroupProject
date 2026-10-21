using System.Text.Json.Serialization;

namespace RoomRum.BLL.DTOs.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RoomRequest(string? Type, string? Title, string? Address, decimal? Area,
    decimal? ElectricityFee, decimal? WaterFee, decimal? ServiceFee, decimal? Deposit,
    decimal? Price, string? Description, string? Rule,
    List<Guid>? AmenityIds, List<string>? ImageUrls);
