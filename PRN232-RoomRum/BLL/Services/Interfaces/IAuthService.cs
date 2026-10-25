using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IAuthService
{
    Task<ApiOutcome> Register(RegisterRequest request);
    Task<ApiOutcome> Login(LoginRequest request);
    Task<ApiOutcome> Me(Guid userId);
    Task<bool> ValidateIdentity(Guid userId, string? role);
}
