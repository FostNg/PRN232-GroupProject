using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IChatService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Messages(Guid userId, Guid id, int page, int pageSize);
    Task<ApiOutcomeResponse> Send(Guid userId, MessageRequest request);
}
