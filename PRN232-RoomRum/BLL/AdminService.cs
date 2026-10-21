using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IAdminService
{
    Task<ApiOutcome> Users(int page, int pageSize);
    Task<ApiOutcome> SetStatus(Guid userId, Guid id, StatusRequest request);
    Task<ApiOutcome> SetRole(Guid userId, Guid id, RoleRequest request);
    Task<ApiOutcome> AddAmenity(AmenityRequest request);
}
public sealed class AdminService(NewRoomRumDbContext db) : IAdminService
{
    public async Task<ApiOutcome> Users(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Users.AsNoTracking()
        .OrderBy(x => x.Name).ThenBy(x => x.UserId).Skip(skip).Take(pageSize)
        .Select(x => new AdminUserSummaryResponse(x.UserId, x.Name, x.Email, x.Phone,
            x.Status, x.Role.RoleName, x.CreatedAt))
        .ToListAsync());
    }
    public async Task<ApiOutcome> SetStatus(Guid userId, Guid id, StatusRequest request)
    {
        if (request.Status is not ("Active" or "Locked")) return ApiOutcome.BadRequest("Invalid status.");
        if (id == userId && request.Status == "Locked") return ApiOutcome.BadRequest("Cannot lock your own account.");
        var user = await db.Users.FindAsync(id);
        if (user is null) return ApiOutcome.NotFound();
        user.Status = request.Status; await db.SaveChangesAsync();
        return ApiOutcome.Ok(new AdminUserStatusResponse(user.UserId, user.Status));
    }
    public async Task<ApiOutcome> SetRole(Guid userId, Guid id, RoleRequest request)
    {
        if (id == userId) return ApiOutcome.BadRequest("Cannot change your own role.");
        var user = await db.Users.FindAsync(id);
        if (user is null) return ApiOutcome.NotFound();
        var role = await db.Roles.SingleOrDefaultAsync(x => x.RoleName == request.Role);
        if (role is null) return ApiOutcome.BadRequest("Unknown role.");
        user.RoleId = role.RoleId; await db.SaveChangesAsync();
        return ApiOutcome.Ok(new AdminUserRoleResponse(user.UserId, role.RoleName));
    }
    public async Task<ApiOutcome> AddAmenity(AmenityRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255) return ApiOutcome.BadRequest("Invalid amenity name.");
        var existing = await db.Amenities.SingleOrDefaultAsync(x => x.AmenityName == name);
        if (existing is not null) return ApiOutcome.Ok(new RoomAmenityResponse(existing.AmenityId, existing.AmenityName));
        var item = new Amenity { AmenityId = Guid.NewGuid(), AmenityName = name };
        db.Amenities.Add(item);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(item).State = EntityState.Detached;
            existing = await db.Amenities.AsNoTracking().SingleOrDefaultAsync(x => x.AmenityName == name);
            if (existing is not null) return ApiOutcome.Ok(new RoomAmenityResponse(existing.AmenityId, existing.AmenityName));
            throw;
        }
        return ApiOutcome.Created(new RoomAmenityResponse(item.AmenityId, item.AmenityName));
    }
}
