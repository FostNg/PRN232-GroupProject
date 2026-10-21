namespace RoomRum.BLL.DTOs.Requests;

public sealed record SharingRequest(Guid RoomId, string? Title, int? Quantity, string? Gender,
    decimal? SharingFee, string? Introduction, string? Rule);
