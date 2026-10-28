namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;

public interface IFavoriteService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Add(Guid userId, Guid roomId);
    Task<ApiOutcome> Remove(Guid userId, Guid roomId);
}
