using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IReportService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, bool isAdmin, int page, int pageSize);
    Task<ApiOutcomeResponse> Create(Guid userId, ReportRequest request);
    Task<ApiOutcomeResponse> Status(Guid id, StatusRequest request);
    Task<ApiOutcomeResponse> Pending(int page, int pageSize);
}
