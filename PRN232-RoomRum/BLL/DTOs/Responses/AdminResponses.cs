namespace RoomRum.BLL.DTOs.Responses;

public sealed record AdminUserSummaryResponse(
    Guid UserId, string Name, string? Email, string? Phone,
    string? Status, string Role, DateTime? CreatedAt);

public sealed record AdminUserStatusResponse(Guid UserId, string? Status);
public sealed record AdminUserRoleResponse(Guid UserId, string Role);
