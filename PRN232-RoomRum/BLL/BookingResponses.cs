namespace RoomRum.BLL.Contracts;

public sealed record AppointmentSummaryResponse(
    Guid AppointmentId, Guid UserId, Guid RoomId, DateOnly AppointmentDate,
    TimeOnly AppointmentTime, string Status, string? Description,
    DateTime CreatedAt, DateTime? UpdatedAt, string? RoomTitle);

public sealed record AppointmentMutationResponse(Guid AppointmentId, string Status);

public sealed record FavoriteSummaryResponse(
    Guid FavoriteId, Guid RoomId, string? Title, decimal? Price, string? ImageUrl);

public sealed record FavoriteMutationResponse(Guid RoomId);
