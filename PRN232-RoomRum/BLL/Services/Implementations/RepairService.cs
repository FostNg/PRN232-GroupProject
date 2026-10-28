namespace RoomRum.BLL.Services.Implementations;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

public sealed class RepairService(IRepairRepository repairs, IRoomTenantRepository tenants) : IRepairService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await repairs.ListForUserAsync(userId, skip, pageSize,
        x => new RepairSummaryResponse(x.MaintenanceId, x.UserId, x.RoomId,
            x.Priority, x.RepairItems, x.Description, x.Status,
            x.ExpectingDate, x.Response)));
    }
    public async Task<ApiOutcome> Create(Guid userId, RepairRequest request)
    {
        if (request.Priority?.Length > 50) return ApiOutcome.BadRequest("Priority exceeds the database column limit.");
        var allowed = await tenants.HasStatusAsync(request.RoomId, userId, "Active");
        if (!allowed) return ApiOutcome.Forbidden();
        var item = new Repairing { MaintenanceId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Priority = request.Priority, RepairItems = request.RepairItems,
            Description = request.Description, ExpectingDate = request.ExpectingDate, Status = "Pending" };
        repairs.Add(item); await repairs.SaveChangesAsync();
        return ApiOutcome.Created(new RepairCreatedResponse(item.MaintenanceId));
    }
    public async Task<ApiOutcome> Respond(Guid userId, Guid id, RepairResponseRequest request)
    {
        var item = await repairs.GetWithRoomAsync(id);
        if (item is null) return ApiOutcome.NotFound();
        if (item.Room.UserId != userId) return ApiOutcome.Forbidden();
        if (request.Status is not ("Pending" or "InProgress" or "Completed" or "Rejected"))
            return ApiOutcome.BadRequest("Invalid status.");
        item.Status = request.Status; item.Response = request.Response;
        await repairs.SaveChangesAsync(); return ApiOutcome.Ok(new RepairStatusResponse(item.MaintenanceId, item.Status));
    }
}
