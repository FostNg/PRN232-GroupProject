using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRepairService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Create(Guid userId, RepairRequest request);
    Task<ApiOutcomeResponse> Respond(Guid userId, Guid id, ConfirmRepairRequest request);
}
