namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

public interface IRepairService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, RepairRequest request);
    Task<ApiOutcome> Respond(Guid userId, Guid id, RepairResponseRequest request);
}
