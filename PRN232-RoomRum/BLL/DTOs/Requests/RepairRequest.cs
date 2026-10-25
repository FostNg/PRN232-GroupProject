namespace RoomRum.BLL.DTOs.Requests;

public sealed record RepairRequest(Guid RoomId, string? Priority, string? RepairItems,
    string? Description, DateOnly? ExpectingDate);
