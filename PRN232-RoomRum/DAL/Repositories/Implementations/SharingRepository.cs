using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class SharingRepository(RoomRumDbContext db) : Repository<Sharing>(db), ISharingRepository
{
    private IQueryable<Sharing> Visible => Db.Sharings.AsNoTracking()
        .Where(x => x.Room.Status == "Rented" || x.Room.Status == "Available" &&
            !x.Room.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active"));

    public Task<List<T>> ListAsync<T>(Guid? roomId, int skip, int take, Expression<Func<Sharing, T>> selector) =>
        Visible.Where(x => roomId == null || x.RoomId == roomId)
            .OrderBy(x => x.SharingId).Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<T?> GetVisibleAsync<T>(Guid id, Expression<Func<Sharing, T>> selector) =>
        Visible.Where(x => x.SharingId == id).Select(selector).FirstOrDefaultAsync();
}
