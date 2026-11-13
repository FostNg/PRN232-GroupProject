using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IAdminService
{
    Task<ApiOutcomeResponse> Users(int page, int pageSize);
    Task<ApiOutcomeResponse> RegistrationFiles(Guid id);
    Task<ApiOutcomeResponse> RegistrationFile(Guid id, Guid fileId);
    Task<ApiOutcomeResponse> SetStatus(Guid userId, Guid id, StatusRequest request);
    Task<ApiOutcomeResponse> SetRole(Guid userId, Guid id, RoleRequest request);
    Task<ApiOutcomeResponse> AddAmenity(AmenityRequest request);
    Task<ApiOutcomeResponse> ReviewRoom(Guid id, StatusRequest request);
    Task<ApiOutcomeResponse> PendingRooms(int page, int pageSize);
}
