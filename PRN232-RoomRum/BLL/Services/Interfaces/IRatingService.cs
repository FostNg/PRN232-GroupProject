namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

public interface IRatingService
{
    Task<ApiOutcome> List(Guid roomId, int page, int pageSize);
    Task<ApiOutcome> Upsert(Guid userId, Guid roomId, RatingRequest request);
}
