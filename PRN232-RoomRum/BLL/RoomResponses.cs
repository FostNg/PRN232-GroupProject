namespace RoomRum.BLL.Contracts;

public sealed record RoomSummaryResponse(
    Guid RoomId, Guid UserId, string? Title, string? Address, string? Type,
    decimal? Price, decimal? Area, string? Status, string? ImageUrl,
    decimal? EstimatedMonthlyCost = null, double? DistanceKm = null,
    double? Latitude = null, double? Longitude = null);

public sealed record RoomSearchResponse(
    IReadOnlyList<RoomSummaryResponse> Items, int Total, int Page, int PageSize);

public sealed record RoomOwnerResponse(Guid UserId, string Name, string? Phone);
public sealed record RoomAmenityResponse(Guid AmenityId, string AmenityName);
public sealed record RoomImageResponse(Guid RoomImageId, string? ImageUrl);
public sealed record RoomMutationResponse(Guid RoomId);

public sealed record RoomMineResponse(
    Guid RoomId, Guid UserId, string? Status, string? Type, string? Title,
    string? Address, decimal? Area, decimal? ElectricityFee, decimal? WaterFee,
    decimal? ServiceFee, decimal? Deposit, decimal? Price, string? Description,
    string? Rule, IReadOnlyList<RoomAmenityResponse> Amenities,
    IReadOnlyList<RoomImageResponse> Images, double? Latitude = null, double? Longitude = null);

public sealed record RoomDetailResponse(
    Guid RoomId, Guid UserId, string? Title, string? Type, string? Status,
    string? Address, decimal? Area, decimal? ElectricityFee, decimal? WaterFee,
    decimal? ServiceFee, decimal? Deposit, decimal? Price, string? Description,
    string? Rule, RoomOwnerResponse Owner,
    IReadOnlyList<RoomAmenityResponse> Amenities,
    IReadOnlyList<RoomImageResponse> Images,
    double? AverageRating, int RatingCount, double? Latitude = null, double? Longitude = null);
