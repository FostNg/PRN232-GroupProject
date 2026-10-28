using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRatingService
{
    Task<ApiOutcomeResponse> List(Guid roomId, int page, int pageSize);
    Task<ApiOutcomeResponse> Upsert(Guid userId, Guid roomId, RatingRequest request);
}
