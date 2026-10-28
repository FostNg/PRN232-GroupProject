namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

public interface IChatService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Messages(Guid userId, Guid id, int page, int pageSize);
    Task<ApiOutcome> Send(Guid userId, MessageRequest request);
}
