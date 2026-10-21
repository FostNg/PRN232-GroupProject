using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IReportService
{
    Task<ApiOutcome> Mine(Guid userId, bool isAdmin, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, ReportRequest request);
    Task<ApiOutcome> Status(Guid id, StatusRequest request);
}
public interface IRepairService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, RepairRequest request);
    Task<ApiOutcome> Respond(Guid userId, Guid id, RepairResponseRequest request);
}
public sealed class ReportService(NewRoomRumDbContext db) : IReportService
{
    public async Task<ApiOutcome> Mine(Guid userId, bool isAdmin, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Reports.AsNoTracking()
            .Where(x => x.UserId == userId || isAdmin)
            .OrderByDescending(x => x.ReportDate).ThenBy(x => x.ReportId)
            .Skip(skip).Take(pageSize)
            .Select(x => new ReportSummaryResponse(x.ReportId, x.UserId,
                x.RoomId, x.Reason, x.ReportDate, x.Status))
            .ToListAsync());
    }
    public async Task<ApiOutcome> Create(Guid userId, ReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return ApiOutcome.BadRequest("Reason is required.");
        if (!await db.Rooms.AnyAsync(x => x.RoomId == request.RoomId)) return ApiOutcome.NotFound();
        var item = new Report { ReportId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Reason = request.Reason.Trim(), Status = "Pending" };
        db.Reports.Add(item); await db.SaveChangesAsync();
        return ApiOutcome.Created(new ReportCreatedResponse(item.ReportId));
    }
    public async Task<ApiOutcome> Status(Guid id, StatusRequest request)
    {
        var item = await db.Reports.FindAsync(id);
        if (item is null) return ApiOutcome.NotFound();
        if (request.Status is not ("Pending" or "Resolved" or "Rejected")) return ApiOutcome.BadRequest("Invalid status.");
        item.Status = request.Status; await db.SaveChangesAsync();
        return ApiOutcome.Ok(new ReportStatusResponse(item.ReportId, item.Status));
    }
}
public sealed class RepairService(NewRoomRumDbContext db) : IRepairService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Repairings.AsNoTracking()
        .Where(x => x.UserId == userId || x.Room.UserId == userId)
        .OrderBy(x => x.MaintenanceId).Skip(skip).Take(pageSize)
        .Select(x => new RepairSummaryResponse(x.MaintenanceId, x.UserId, x.RoomId,
            x.Priority, x.RepairItems, x.Description, x.Status,
            x.ExpectingDate, x.Response, x.Room.Title, x.User.Name))
        .ToListAsync());
    }
    public async Task<ApiOutcome> Create(Guid userId, RepairRequest request)
    {
        if (request.Priority?.Length > 50) return ApiOutcome.BadRequest("Priority exceeds the database column limit.");
        var allowed = await db.RoomTenants.AnyAsync(x => x.RoomId == request.RoomId && x.UserId == userId && x.Status == "Active");
        if (!allowed) return ApiOutcome.Forbidden();
        var item = new Repairing { MaintenanceId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Priority = request.Priority, RepairItems = request.RepairItems,
            Description = request.Description, ExpectingDate = request.ExpectingDate, Status = "Pending" };
        db.Repairings.Add(item); await db.SaveChangesAsync();
        return ApiOutcome.Created(new RepairCreatedResponse(item.MaintenanceId));
    }
    public async Task<ApiOutcome> Respond(Guid userId, Guid id, RepairResponseRequest request)
    {
        var item = await db.Repairings.Include(x => x.Room).SingleOrDefaultAsync(x => x.MaintenanceId == id);
        if (item is null) return ApiOutcome.NotFound();
        if (item.Room.UserId != userId) return ApiOutcome.Forbidden();
        if (request.Status is not ("Pending" or "InProgress" or "Completed" or "Rejected"))
            return ApiOutcome.BadRequest("Invalid status.");
        item.Status = request.Status; item.Response = request.Response;
        await db.SaveChangesAsync(); return ApiOutcome.Ok(new RepairStatusResponse(item.MaintenanceId, item.Status));
    }
}
