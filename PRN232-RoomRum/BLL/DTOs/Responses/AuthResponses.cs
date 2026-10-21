namespace RoomRum.BLL.DTOs.Responses;

public sealed record RegistrationResponse(
    Guid UserId, string Name, string? Email, string Role, string Status);

public sealed record AuthSessionResponse(
    Guid UserId, string Name, string? Email, string Role, string Token);

public sealed record CurrentUserResponse(
    Guid UserId, string Name, string? Email, string? Phone,
    DateOnly? Dob, string? Gender, string? ImageUrl,
    string? Status, string Role);
