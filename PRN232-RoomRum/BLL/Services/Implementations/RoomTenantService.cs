using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RoomTenantService(IRoomTenantRepository tenants, IRoomRepository rooms,
    IUserRepository users) : IRoomTenantService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await tenants.ListForUserAsync(userId, skip, pageSize,
            x => new RoomTenantSummaryResponse(x.RoomId, x.UserId, x.Status,
                x.HiredAt, x.LeftAt, x.Room.Title)));
    }
    public async Task<ApiOutcome> Add(Guid userId, Guid roomId, TenantRequest request)
    {
        var room = await rooms.FindAsync(roomId);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId) return ApiOutcome.Forbidden();
        if (room.Status is not ("Available" or "Rented"))
            return ApiOutcome.Conflict("Only an available or rented room can receive tenants.");
        if (room.UserId == request.UserId || !await users.ExistsAsync(request.UserId))
            return ApiOutcome.BadRequest("Invalid tenant.");
        var existing = await tenants.FindAsync(roomId, request.UserId);
        if (existing is not null)
        {
            if (existing.Status is not ("Ended" or "Cancelled"))
                return ApiOutcome.Conflict("This user already has a pending or active tenancy for the room.");
            var now = DateTime.Now;
            var restarted = await tenants.RestartAsync(existing, "Pending", now);
            if (!restarted)
                return ApiOutcome.Conflict("Tenancy status changed. Reload it and try again.");
            return ApiOutcome.Created(new RoomTenantCreatedResponse(roomId, request.UserId));
        }
        tenants.Add(new RoomTenant { RoomId = roomId, UserId = request.UserId, Status = "Pending" });
        await tenants.SaveChangesAsync(); return ApiOutcome.Created(new RoomTenantCreatedResponse(roomId, request.UserId));
    }
    public async Task<ApiOutcome> Accept(Guid userId, Guid roomId)
    {
        var now = DateTime.Now;
        var changed = await tenants.AcceptAsync(roomId, userId, "Pending", "Active", now);
        if (changed == 0) return ApiOutcome.Conflict("No pending tenancy invitation for this room.");
        return ApiOutcome.Ok(new RoomTenantCreatedResponse(roomId, userId));
    }
    public async Task<ApiOutcome> End(Guid userId, Guid roomId, Guid tenantId)
    {
        var item = await tenants.GetWithRoomAsync(roomId, tenantId);
        if (item is null) return ApiOutcome.NotFound();
        if (item.Room.UserId != userId && tenantId != userId) return ApiOutcome.Forbidden();
        if (item.Status is not ("Active" or "Pending"))
            return ApiOutcome.Conflict("Tenancy is neither active nor pending.");
        var nextStatus = item.Status == "Pending" ? "Cancelled" : "Ended";
        var changed = await tenants.EndAsync(roomId, tenantId, item.Status, nextStatus,
            nextStatus == "Ended" ? DateTime.Now : (DateTime?)null);
        if (changed == 0) return ApiOutcome.Conflict("Tenancy status changed. Reload it and try again.");
        return ApiOutcome.Ok(new RoomTenantEndedResponse(item.RoomId, item.UserId, nextStatus));
    }

    public async Task<ApiOutcome> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        var current = await tenants.GetCurrentAsync(roomId, tenantId,
            x => new { x.Status, x.HiredAt, x.LeftAt, OwnerId = x.Room.UserId });
        if (current is null) return ApiOutcome.NotFound();
        if (userId != tenantId && userId != current.OwnerId) return ApiOutcome.Forbidden();

        var offset = (page - 1) * pageSize;
        var history = await tenants.GetHistoryAsync(roomId, tenantId, offset, pageSize,
            x => new RoomTenantPeriodResponse(x.HistoryId, x.Status,
                x.HiredAt, x.LeftAt, x.ArchivedAt, false));
        var archivedCount = history.Total;
        var items = history.Items;
        if (offset <= archivedCount && (long)offset + pageSize > archivedCount)
            items.Add(new RoomTenantPeriodResponse(null, current.Status,
                current.HiredAt, current.LeftAt, null, true));
        return ApiOutcome.Ok(new RoomTenantHistoryPageResponse(items, archivedCount + 1, page, pageSize));
    }
}
