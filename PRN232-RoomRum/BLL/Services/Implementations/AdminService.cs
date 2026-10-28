using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class AdminService(IUserRepository users, IRoomRepository rooms, IAmenityRepository amenities) : IAdminService
{
    public async Task<ApiOutcomeResponse> Users(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await users.ListAsync(skip, pageSize,
            x => new UserResponse
            {
                UserId = x.UserId,
                Name = x.Name,
                Email = x.Email,
                Phone = x.Phone,
                Status = x.Status,
                Role = x.Role.RoleName,
                CreatedAt = x.CreatedAt
            }));
    }
    public async Task<ApiOutcomeResponse> SetStatus(Guid userId, Guid id, StatusRequest request)
    {
        if (request.Status is not ("Active" or "Inactive")) return ApiOutcomeResponse.BadRequest("Invalid status.");
        if (id == userId && request.Status == "Inactive") return ApiOutcomeResponse.BadRequest("Cannot deactivate your own account.");
        var user = await users.FindAsync(id);
        if (user is null) return ApiOutcomeResponse.NotFound();
        user.Status = request.Status; await users.SaveChangesAsync();
        return ApiOutcomeResponse.Ok();
    }
    public async Task<ApiOutcomeResponse> SetRole(Guid userId, Guid id, RoleRequest request)
    {
        if (id == userId) return ApiOutcomeResponse.BadRequest("Cannot change your own role.");
        var user = await users.FindAsync(id);
        if (user is null) return ApiOutcomeResponse.NotFound();
        var role = await users.GetRoleAsync(request.Role);
        if (role is null) return ApiOutcomeResponse.BadRequest("Unknown role.");
        user.RoleId = role.RoleId; await users.SaveChangesAsync();
        return ApiOutcomeResponse.Ok();
    }
    public async Task<ApiOutcomeResponse> AddAmenity(AmenityRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255) return ApiOutcomeResponse.BadRequest("Invalid amenity name.");
        var result = await amenities.GetOrCreateAsync(name);
        var response = new RoomAmenityResponse { AmenityId = result.Item.AmenityId, AmenityName = result.Item.AmenityName };
        return result.Created ? ApiOutcomeResponse.Created(response) : ApiOutcomeResponse.Ok(response);
    }
    public async Task<ApiOutcomeResponse> ReviewRoom(Guid id, StatusRequest request)
    {
        if (request.Status is not ("Available" or "Cancelled"))
            return ApiOutcomeResponse.BadRequest("Review status must be Available or Cancelled.");
        var changed = await rooms.ReviewAsync(id, "Pending", request.Status);
        if (changed == 1) return ApiOutcomeResponse.Ok();
        if (!await rooms.ExistsAsync(id)) return ApiOutcomeResponse.NotFound();
        return ApiOutcomeResponse.Conflict("Only a pending room can be reviewed.");
    }
    public async Task<ApiOutcomeResponse> PendingRooms(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await rooms.ListPendingAsync(skip, pageSize,
            x => new PendingRoomResponse
            {
                RoomId = x.RoomId,
                UserId = x.UserId,
                Title = x.Title,
                Address = x.Address,
                Status = x.Status!
            }));
    }
}
