using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface ISharingService
{
    Task<ApiOutcomeResponse> List(Guid? roomId, int page, int pageSize);
    Task<ApiOutcomeResponse> Create(Guid userId, SharingRequest request);
    Task<ApiOutcomeResponse> Delete(Guid userId, bool isAdmin, Guid id);
}
