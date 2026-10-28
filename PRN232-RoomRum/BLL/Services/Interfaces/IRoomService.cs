using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRoomService
{
    Task<ApiOutcomeResponse> Search(RoomSearchRequest request);
    Task<ApiOutcomeResponse> Detail(Guid id, Guid? viewerId);
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Create(Guid userId, RoomRequest request);
    Task<ApiOutcomeResponse> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request);
    Task<ApiOutcomeResponse> Hide(Guid userId, bool isAdmin, Guid id);
    Task<ApiOutcomeResponse> Amenities(int page, int pageSize);
}
