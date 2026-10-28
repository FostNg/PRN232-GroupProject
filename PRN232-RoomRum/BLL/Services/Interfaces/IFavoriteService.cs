using RoomRum.BLL.DTOs.Responses;

namespace RoomRum.BLL.Services.Interfaces;

public interface IFavoriteService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Add(Guid userId, Guid roomId);
    Task<ApiOutcomeResponse> Remove(Guid userId, Guid roomId);
}
