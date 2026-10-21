using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRoomService
{
    Task<ApiOutcome> Search(RoomSearchRequest request);
    Task<ApiOutcome> Detail(Guid id, Guid? viewerId);
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, RoomRequest request);
    Task<ApiOutcome> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request);
    Task<ApiOutcome> Hide(Guid userId, bool isAdmin, Guid id);
    Task<ApiOutcome> Amenities(int page, int pageSize);
}
