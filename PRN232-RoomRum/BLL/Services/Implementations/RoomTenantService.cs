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
    public async Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await tenants.ListForUserAsync(userId, skip, pageSize,
            x => new RoomTenantResponse
            {
                RoomId = x.RoomId,
                UserId = x.UserId,
                Status = x.Status,
                HiredAt = x.HiredAt,
                LeftAt = x.LeftAt,
                RoomTitle = x.Room.Title,
                TenantName = x.User.Name
            }));
    }
    public async Task<ApiOutcomeResponse> Add(Guid userId, Guid roomId, TenantRequest request)
    {
        var room = await rooms.FindAsync(roomId);
        if (room is null) return ApiOutcomeResponse.NotFound();
        if (room.UserId != userId) return ApiOutcomeResponse.Forbidden();
        if (room.Status is not ("Available" or "Rented"))
            return ApiOutcomeResponse.Conflict("Only an available or rented room can receive tenants.");
        if (room.UserId == request.UserId || !await users.ExistsAsync(request.UserId))
            return ApiOutcomeResponse.BadRequest("Invalid tenant.");
        var existing = await tenants.FindAsync(roomId, request.UserId);
        if (existing is not null)
        {
            if (existing.Status is not ("Ended" or "Cancelled"))
                return ApiOutcomeResponse.Conflict("This user already has a pending or active tenancy for the room.");
            var now = DateTime.Now;
            var restarted = await tenants.RestartAsync(existing, "Pending", now);
            if (!restarted)
                return ApiOutcomeResponse.Conflict("Tenancy status changed. Reload it and try again.");
            return ApiOutcomeResponse.Created();
        }
        tenants.Add(new RoomTenant { RoomId = roomId, UserId = request.UserId, Status = "Pending" });
        await tenants.SaveChangesAsync(); return ApiOutcomeResponse.Created();
    }
    public async Task<ApiOutcomeResponse> AddByEmail(Guid userId, Guid roomId, TenantInviteByEmailRequest request)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            return ApiOutcomeResponse.BadRequest("Email người thuê không hợp lệ.");

        var room = await rooms.FindAsync(roomId);
        if (room is null) return ApiOutcomeResponse.NotFound();
        if (room.UserId != userId) return ApiOutcomeResponse.Forbidden();

        var tenant = await users.GetByEmailWithRoleAsync(email);
        if (tenant is null || tenant.Role.RoleName != "Tenant" || tenant.Status != "Active")
            return ApiOutcomeResponse.NotFound("Không tìm thấy tài khoản người thuê đang hoạt động với email này.");

        var outcome = await Add(userId, roomId, new TenantRequest { UserId = tenant.UserId });
        return outcome.StatusCode == 409
            ? ApiOutcomeResponse.Conflict("Người thuê này đã được mời hoặc đang thuê phòng.")
            : outcome;
    }
    public async Task<ApiOutcomeResponse> Accept(Guid userId, Guid roomId)
    {
        var now = DateTime.Now;
        var changed = await tenants.AcceptAsync(roomId, userId, "Pending", "Active", now);
        if (changed == 0) return ApiOutcomeResponse.Conflict("No pending tenancy invitation for this room.");
        return ApiOutcomeResponse.Ok();
    }
    public async Task<ApiOutcomeResponse> End(Guid userId, Guid roomId, Guid tenantId)
    {
        var item = await tenants.GetWithRoomAsync(roomId, tenantId);
        if (item is null) return ApiOutcomeResponse.NotFound();
        if (item.Room.UserId != userId && tenantId != userId) return ApiOutcomeResponse.Forbidden();
        if (item.Status is not ("Active" or "Pending"))
            return ApiOutcomeResponse.Conflict("Tenancy is neither active nor pending.");
        var nextStatus = item.Status == "Pending" ? "Cancelled" : "Ended";
        var changed = await tenants.EndAsync(roomId, tenantId, item.Status, nextStatus,
            nextStatus == "Ended" ? DateTime.Now : (DateTime?)null);
        if (changed == 0) return ApiOutcomeResponse.Conflict("Tenancy status changed. Reload it and try again.");
        return ApiOutcomeResponse.Ok();
    }

    public async Task<ApiOutcomeResponse> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        var current = await tenants.GetCurrentAsync(roomId, tenantId,
            x => new { x.Status, x.HiredAt, x.LeftAt, OwnerId = x.Room.UserId });
        if (current is null) return ApiOutcomeResponse.NotFound();
        if (userId != tenantId && userId != current.OwnerId) return ApiOutcomeResponse.Forbidden();

        var offset = (page - 1) * pageSize;
        var history = await tenants.GetHistoryAsync(roomId, tenantId, offset, pageSize,
            x => new RoomTenantPeriodResponse
            {
                HistoryId = x.HistoryId,
                Status = x.Status,
                HiredAt = x.HiredAt,
                LeftAt = x.LeftAt,
                ArchivedAt = x.ArchivedAt,
                IsCurrent = false
            });
        var archivedCount = history.Total;
        var items = history.Items;
        if (offset <= archivedCount && (long)offset + pageSize > archivedCount)
            items.Add(new RoomTenantPeriodResponse
            {
                HistoryId = null,
                Status = current.Status,
                HiredAt = current.HiredAt,
                LeftAt = current.LeftAt,
                ArchivedAt = null,
                IsCurrent = true
            });
        return ApiOutcomeResponse.Ok(new RoomTenantHistoryPageResponse
        {
            Items = items,
            Total = archivedCount + 1,
            Page = page,
            PageSize = pageSize
        });
    }
}
