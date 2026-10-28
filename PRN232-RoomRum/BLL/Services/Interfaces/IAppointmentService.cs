using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IAppointmentService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Create(Guid userId, AppointmentRequest request);
    Task<ApiOutcomeResponse> UpdateStatus(Guid userId, Guid id, StatusRequest request);
}
