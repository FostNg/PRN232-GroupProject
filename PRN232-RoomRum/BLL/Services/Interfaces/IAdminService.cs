using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IAdminService
{
    Task<ApiOutcome> Users(int page, int pageSize);
    Task<ApiOutcome> SetStatus(Guid userId, Guid id, StatusRequest request);
    Task<ApiOutcome> SetRole(Guid userId, Guid id, RoleRequest request);
    Task<ApiOutcome> AddAmenity(AmenityRequest request);
    Task<ApiOutcome> ReviewRoom(Guid id, StatusRequest request);
    Task<ApiOutcome> PendingRooms(int page, int pageSize);
}
