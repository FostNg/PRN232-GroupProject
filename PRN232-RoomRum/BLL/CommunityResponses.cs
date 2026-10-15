namespace RoomRum.BLL.Contracts;

public sealed record SharingSummaryResponse(
    Guid SharingId, Guid UserId, Guid RoomId, string? Title, int? Quantity,
    string? Gender, decimal? SharingFee, string? Introduction, string? Rule,
    string? RoomTitle, string Author);

public sealed record SharingMutationResponse(Guid SharingId);

public sealed record RatingSummaryResponse(
    Guid RatingId, int Rating, string? Comment, DateTime RatingDate, string User);

public sealed record RatingMutationResponse(Guid RatingId);

public sealed record RoomTenantSummaryResponse(
    Guid RoomId, Guid UserId, string Status, DateTime HiredAt,
    DateTime? LeftAt, string? RoomTitle);

public sealed record RoomTenantCreatedResponse(Guid RoomId, Guid UserId);
public sealed record RoomTenantEndedResponse(Guid RoomId, Guid UserId, string Status);
public sealed record RoomTenantPeriodResponse(
    Guid? HistoryId, string Status, DateTime HiredAt, DateTime? LeftAt,
    DateTime? ArchivedAt, bool IsCurrent);
public sealed record RoomTenantHistoryPageResponse(
    List<RoomTenantPeriodResponse> Items, int Total, int Page, int PageSize);
