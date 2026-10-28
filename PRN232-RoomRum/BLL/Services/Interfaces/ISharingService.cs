namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

public interface ISharingService
{
    Task<ApiOutcome> List(Guid? roomId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, SharingRequest request);
    Task<ApiOutcome> Delete(Guid userId, bool isAdmin, Guid id);
}
