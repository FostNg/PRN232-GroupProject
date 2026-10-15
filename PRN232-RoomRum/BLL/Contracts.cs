namespace RoomRum.BLL.Contracts;

public sealed record RegisterRequest(string Name, string Email, string Password, string? Phone, string Role);
public sealed record LoginRequest(string Email, string Password);
public sealed record RoomRequest(string? Type, string? Title, string? Address, decimal? Area,
    decimal? ElectricityFee, decimal? WaterFee, decimal? ServiceFee, decimal? Deposit,
    decimal? Price, string? Description, string? Rule, string? Status,
    List<Guid>? AmenityIds, List<string>? ImageUrls);
public sealed record AppointmentRequest(Guid RoomId, DateOnly AppointmentDate, TimeOnly AppointmentTime, string? Description);
public sealed record StatusRequest(string Status);
public sealed record SharingRequest(Guid RoomId, string? Title, int? Quantity, string? Gender,
    decimal? SharingFee, string? Introduction, string? Rule);
public sealed record RatingRequest(int Rating, string? Comment);
public sealed record ReportRequest(Guid RoomId, string Reason);
public sealed record RepairRequest(Guid RoomId, string? Priority, string? RepairItems,
    string? Description, DateOnly? ExpectingDate);
public sealed record RepairResponseRequest(string Status, string? Response);
public sealed record TenantRequest(Guid UserId);
public sealed record MessageRequest(Guid? ConversationId, Guid Receiver, string Content);
public sealed record ImageRequest(string ImageUrl);
public sealed record AmenityRequest(string Name);
public sealed record RoleRequest(string Role);
