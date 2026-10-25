namespace RoomRum.BLL.Contracts;

public sealed record ReportSummaryResponse(
    Guid ReportId, Guid UserId, Guid RoomId, string? Reason,
    DateTime? ReportDate, string? Status);

public sealed record ReportCreatedResponse(Guid ReportId);
public sealed record ReportStatusResponse(Guid ReportId, string? Status);

public sealed record RepairSummaryResponse(
    Guid MaintenanceId, Guid UserId, Guid RoomId, string? Priority,
    string? RepairItems, string? Description, string? Status,
    DateOnly? ExpectingDate, string? Response, string? RoomTitle, string TenantName);

public sealed record RepairCreatedResponse(Guid MaintenanceId);
public sealed record RepairStatusResponse(Guid MaintenanceId, string? Status);
