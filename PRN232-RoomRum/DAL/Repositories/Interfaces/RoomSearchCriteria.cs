namespace RoomRum.DAL.Repositories.Interfaces;

public sealed record RoomSearchCriteria(
    string? Q, string? Type, decimal? MinPrice, decimal? MaxPrice,
    decimal? MinArea, decimal? MaxArea, decimal? MaxDeposit,
    decimal? MaxMoveInCost, decimal? MaxMonthlyCost, int Occupants,
    int MonthlyKwh, IReadOnlyList<Guid> AmenityIds, double? Latitude,
    double? Longitude, double? MaxDistanceKm, string? Sort, int Skip, int Take);

public sealed record RoomSearchItem(
    Guid RoomId, Guid UserId, string? Title, string? Address, string? Type,
    decimal? Price, decimal? Area, string? Status, string? ImageUrl,
    decimal? ElectricityFee, decimal? WaterFee, decimal? ServiceFee,
    double? Latitude, double? Longitude, double? DistanceKm);

public sealed record RoomSearchResult(IReadOnlyList<RoomSearchItem> Items, int Total);
