using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class FavoriteRepository(RoomRumDbContext db) : IFavoriteRepository
{
    public Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Favorite, T>> selector) =>
        db.Favorites.AsNoTracking().Where(x => x.UserId == userId &&
            (x.Room.Status != "Available" ||
             !x.Room.RoomTenants.Any(t => t.Status == "Pending") ||
             x.Room.UserId == userId ||
             x.Room.RoomTenants.Any(t => t.Status == "Pending" && t.UserId == userId)))
            .OrderBy(x => x.FavoriteId).Skip(skip).Take(take).Select(selector).ToListAsync();

    public async Task<bool> AddIfMissingAsync(Favorite favorite)
    {
        if (await ExistsAsync(favorite.UserId, favorite.RoomId)) return false;
        db.Favorites.Add(favorite);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(favorite).State = EntityState.Detached;
            if (await ExistsAsync(favorite.UserId, favorite.RoomId)) return false;
            throw;
        }
        return true;
    }

    private Task<bool> ExistsAsync(Guid userId, Guid roomId) =>
        db.Favorites.AnyAsync(x => x.UserId == userId && x.RoomId == roomId);

    public async Task RemoveAsync(Guid userId, Guid roomId)
    {
        var records = await db.Favorites.Where(x => x.UserId == userId && x.RoomId == roomId).ToListAsync();
        db.Favorites.RemoveRange(records);
        await db.SaveChangesAsync();
    }
}
