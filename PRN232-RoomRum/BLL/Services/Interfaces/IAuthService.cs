using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IAuthService
{
    Task<ApiOutcomeResponse> Register(RegisterRequest request, IReadOnlyList<RegistrationFile>? files = null);
    Task<ApiOutcomeResponse> SendVerificationEmail(LoginRequest request);
    Task<ApiOutcomeResponse> VerifyEmail(Guid userId, string? token);
    Task<ApiOutcomeResponse> Login(LoginRequest request);
    Task<ApiOutcomeResponse> Me(Guid userId);
    Task<bool> ValidateIdentity(Guid userId, string? role);
}
