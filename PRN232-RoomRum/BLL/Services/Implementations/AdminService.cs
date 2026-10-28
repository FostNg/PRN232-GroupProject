namespace RoomRum.BLL.Services.Implementations;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

public sealed class AdminService(IUserRepository users, IRoomRepository rooms, IAmenityRepository amenities) : IAdminService
{
    public async Task<ApiOutcome> Users(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await users.ListAsync(skip, pageSize,
            x => new AdminUserSummaryResponse(x.UserId, x.Name, x.Email, x.Phone,
                x.Status, x.Role.RoleName, x.CreatedAt)));
    }
    public async Task<ApiOutcome> SetStatus(Guid userId, Guid id, StatusRequest request)
    {
        if (request.Status is not ("Active" or "Inactive")) return ApiOutcome.BadRequest("Invalid status.");
        if (id == userId && request.Status == "Inactive") return ApiOutcome.BadRequest("Cannot deactivate your own account.");
        var user = await users.FindAsync(id);
        if (user is null) return ApiOutcome.NotFound();
        user.Status = request.Status; await users.SaveChangesAsync();
        return ApiOutcome.Ok(new AdminUserStatusResponse(user.UserId, user.Status));
    }
    public async Task<ApiOutcome> SetRole(Guid userId, Guid id, RoleRequest request)
    {
        if (id == userId) return ApiOutcome.BadRequest("Cannot change your own role.");
        var user = await users.FindAsync(id);
        if (user is null) return ApiOutcome.NotFound();
        var role = await users.GetRoleAsync(request.Role);
        if (role is null) return ApiOutcome.BadRequest("Unknown role.");
        user.RoleId = role.RoleId; await users.SaveChangesAsync();
        return ApiOutcome.Ok(new AdminUserRoleResponse(user.UserId, role.RoleName));
    }
    public async Task<ApiOutcome> AddAmenity(AmenityRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255) return ApiOutcome.BadRequest("Invalid amenity name.");
        var result = await amenities.GetOrCreateAsync(name);
        var response = new RoomAmenityResponse(result.Item.AmenityId, result.Item.AmenityName);
        return result.Created ? ApiOutcome.Created(response) : ApiOutcome.Ok(response);
    }
    public async Task<ApiOutcome> ReviewRoom(Guid id, StatusRequest request)
    {
        if (request.Status is not ("Available" or "Cancelled"))
            return ApiOutcome.BadRequest("Review status must be Available or Cancelled.");
        var changed = await rooms.ReviewAsync(id, "Pending", request.Status);
        if (changed == 1) return ApiOutcome.Ok(new RoomStatusResponse(id, request.Status));
        if (!await rooms.ExistsAsync(id)) return ApiOutcome.NotFound();
        return ApiOutcome.Conflict("Only a pending room can be reviewed.");
    }
    public async Task<ApiOutcome> PendingRooms(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await rooms.ListPendingAsync(skip, pageSize,
            x => new PendingRoomResponse(x.RoomId, x.UserId, x.Title, x.Address, x.Status!)));
    }
}
