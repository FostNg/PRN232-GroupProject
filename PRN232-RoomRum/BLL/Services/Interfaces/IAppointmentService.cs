namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

public interface IAppointmentService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, AppointmentRequest request);
    Task<ApiOutcome> UpdateStatus(Guid userId, Guid id, StatusRequest request);
}
