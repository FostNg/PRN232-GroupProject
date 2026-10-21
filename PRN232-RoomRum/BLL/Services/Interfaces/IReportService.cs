using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IReportService
{
    Task<ApiOutcome> Mine(Guid userId, bool isAdmin, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, ReportRequest request);
    Task<ApiOutcome> Status(Guid id, StatusRequest request);
    Task<ApiOutcome> Pending(int page, int pageSize);
}
