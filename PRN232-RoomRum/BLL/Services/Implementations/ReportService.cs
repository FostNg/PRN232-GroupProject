using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class ReportService(IReportRepository reports, IRoomRepository rooms) : IReportService
{
    public async Task<ApiOutcomeResponse> Mine(Guid userId, bool isAdmin, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await reports.ListAsync(userId, isAdmin, skip, pageSize,
            x => new ReportResponse
            {
                ReportId = x.ReportId,
                UserId = x.UserId,
                RoomId = x.RoomId,
                Reason = x.Reason,
                ReportDate = x.ReportDate,
                Status = x.Status,
                RoomTitle = x.Room.Title
            }));
    }
    public async Task<ApiOutcomeResponse> Create(Guid userId, ReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return ApiOutcomeResponse.BadRequest("Reason is required.");
        if (!await rooms.ExistsAsync(request.RoomId)) return ApiOutcomeResponse.NotFound();
        var item = new Report { ReportId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Reason = request.Reason.Trim(), Status = "Pending" };
        reports.Add(item); await reports.SaveChangesAsync();
        return ApiOutcomeResponse.Created();
    }
    public async Task<ApiOutcomeResponse> Status(Guid id, StatusRequest request)
    {
        if (request.Status is not ("Resolved" or "Rejected"))
            return ApiOutcomeResponse.BadRequest("Review status must be Resolved or Rejected.");
        var changed = await reports.ReviewAsync(id, request.Status);
        if (changed == 1) return ApiOutcomeResponse.Ok();
        if (!await reports.ExistsAsync(id)) return ApiOutcomeResponse.NotFound();
        return ApiOutcomeResponse.Conflict("Only a pending report can be reviewed.");
    }

    public async Task<ApiOutcomeResponse> Pending(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await reports.ListPendingAsync(skip, pageSize,
            x => new ReportResponse
            {
                ReportId = x.ReportId,
                UserId = x.UserId,
                RoomId = x.RoomId,
                Reason = x.Reason,
                ReportDate = x.ReportDate,
                Status = x.Status,
                RoomTitle = x.Room.Title
            }));
    }
}
