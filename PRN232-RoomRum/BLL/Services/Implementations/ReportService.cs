namespace RoomRum.BLL.Services.Implementations;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

public sealed class ReportService(IReportRepository reports, IRoomRepository rooms) : IReportService
{
    public async Task<ApiOutcome> Mine(Guid userId, bool isAdmin, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await reports.ListAsync(userId, isAdmin, skip, pageSize,
            x => new ReportSummaryResponse(x.ReportId, x.UserId,
                x.RoomId, x.Reason, x.ReportDate, x.Status)));
    }
    public async Task<ApiOutcome> Create(Guid userId, ReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return ApiOutcome.BadRequest("Reason is required.");
        if (!await rooms.ExistsAsync(request.RoomId)) return ApiOutcome.NotFound();
        var item = new Report { ReportId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Reason = request.Reason.Trim(), Status = "Pending" };
        reports.Add(item); await reports.SaveChangesAsync();
        return ApiOutcome.Created(new ReportCreatedResponse(item.ReportId));
    }
    public async Task<ApiOutcome> Status(Guid id, StatusRequest request)
    {
        if (request.Status is not ("Resolved" or "Rejected"))
            return ApiOutcome.BadRequest("Review status must be Resolved or Rejected.");
        var changed = await reports.ReviewAsync(id, request.Status);
        if (changed == 1) return ApiOutcome.Ok(new ReportStatusResponse(id, request.Status));
        if (!await reports.ExistsAsync(id)) return ApiOutcome.NotFound();
        return ApiOutcome.Conflict("Only a pending report can be reviewed.");
    }

    public async Task<ApiOutcome> Pending(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await reports.ListPendingAsync(skip, pageSize,
            x => new ReportSummaryResponse(x.ReportId, x.UserId,
                x.RoomId, x.Reason, x.ReportDate, x.Status)));
    }
}
