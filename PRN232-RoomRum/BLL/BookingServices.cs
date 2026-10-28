using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IAppointmentService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, AppointmentRequest request);
    Task<ApiOutcome> UpdateStatus(Guid userId, Guid id, StatusRequest request);
}

public interface IFavoriteService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Add(Guid userId, Guid roomId);
    Task<ApiOutcome> Remove(Guid userId, Guid roomId);
}
public sealed class AppointmentService(NewRoomRumDbContext db) : IAppointmentService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Appointments.AsNoTracking()
        .Where(x => x.UserId == userId || x.Room.UserId == userId)
        .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime)
        .ThenBy(x => x.AppointmentId).Skip(skip).Take(pageSize)
        .Select(x => new AppointmentSummaryResponse(x.AppointmentId, x.UserId, x.RoomId,
            x.AppointmentDate, x.AppointmentTime, x.Status, x.Description,
            x.CreatedAt, x.UpdatedAt, x.Room.Title, x.User.Name))
        .ToListAsync());
    }
    public async Task<ApiOutcome> Create(Guid userId, AppointmentRequest request)
    {
        var room = await db.Rooms.FindAsync(request.RoomId);
        if (room is null || room.Status != "Available") return ApiOutcome.NotFound();
        if (room.UserId == userId) return ApiOutcome.BadRequest("Cannot book your own room.");
        if (request.AppointmentDate.ToDateTime(request.AppointmentTime) <= DateTime.Now)
            return ApiOutcome.BadRequest("Appointment must be in the future.");
        var item = new Appointment { AppointmentId = Guid.NewGuid(), UserId = userId,
            RoomId = room.RoomId, AppointmentDate = request.AppointmentDate,
            AppointmentTime = request.AppointmentTime, Description = request.Description,
            Status = "Pending" };
        db.Appointments.Add(item);
        await db.SaveChangesAsync();
        return ApiOutcome.Created(new AppointmentMutationResponse(item.AppointmentId, item.Status));
    }
    public async Task<ApiOutcome> Add(Guid userId, Guid roomId)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null || room.Status != "Available") return ApiOutcome.NotFound();
        if (await db.Favorites.AnyAsync(x => x.UserId == userId && x.RoomId == roomId)) return ApiOutcome.Ok(new FavoriteMutationResponse(roomId));
        var favorite = new Favorite { FavoriteId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
        db.Favorites.Add(favorite);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(favorite).State = EntityState.Detached;
            if (await db.Favorites.AnyAsync(x => x.UserId == userId && x.RoomId == roomId))
                return ApiOutcome.Ok(new FavoriteMutationResponse(roomId));
            throw;
        }
        return ApiOutcome.Created(new FavoriteMutationResponse(roomId));
    }
    public async Task<ApiOutcome> UpdateStatus(Guid userId, Guid id, StatusRequest request)
    {
        var item = await db.Appointments.Include(x => x.Room).SingleOrDefaultAsync(x => x.AppointmentId == id);
        if (item is null) return ApiOutcome.NotFound();
        var owner = item.Room.UserId == userId;
        var guest = item.UserId == userId;
        var valid = item.Status == "Pending" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status is "Confirmed" or "Rejected")) ||
            item.Status == "Confirmed" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status == "Completed"));
        if (!valid) return ApiOutcome.Conflict("Invalid status transition or permission.");
        var changed = await db.Appointments
            .Where(x => x.AppointmentId == id && x.Status == item.Status)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Status)
                .SetProperty(x => x.UpdatedAt, DateTime.Now));
        if (changed == 0) return ApiOutcome.Conflict("Appointment status changed. Reload it and try again.");
        return ApiOutcome.Ok(new AppointmentMutationResponse(item.AppointmentId, request.Status));
    }
}
public sealed class FavoriteService(NewRoomRumDbContext db) : IFavoriteService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Favorites.AsNoTracking()
        .Where(x => x.UserId == userId)
        .OrderBy(x => x.FavoriteId).Skip(skip).Take(pageSize)
        .Select(x => new FavoriteSummaryResponse(x.FavoriteId, x.RoomId, x.Room.Title,
            x.Room.Price, x.Room.RoomImages.Select(i => i.ImageUrl).FirstOrDefault()))
        .ToListAsync());
    }
    
    public async Task<ApiOutcome> Remove(Guid userId, Guid roomId)
    {
        var records = await db.Favorites.Where(x => x.UserId == userId && x.RoomId == roomId).ToListAsync();
        db.Favorites.RemoveRange(records);
        await db.SaveChangesAsync();
        return ApiOutcome.NoContent();
    }
}
