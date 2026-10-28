using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class RepairRepository(RoomRumDbContext db) : Repository<Repairing>(db), IRepairRepository
{
    public Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Repairing, T>> selector) =>
        Db.Repairings.AsNoTracking().Where(x => x.UserId == userId || x.Room.UserId == userId)
            .OrderBy(x => x.MaintenanceId).Skip(skip).Take(take).Select(selector).ToListAsync();
    public Task<Repairing?> GetWithRoomAsync(Guid id) =>
        Db.Repairings.Include(x => x.Room).SingleOrDefaultAsync(x => x.MaintenanceId == id);
}
