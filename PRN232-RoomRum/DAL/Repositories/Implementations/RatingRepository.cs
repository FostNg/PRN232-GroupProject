using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class RatingRepository(RoomRumDbContext db) : Repository<RoomRating>(db), IRatingRepository
{
    public Task<List<T>> ListAsync<T>(Guid roomId, int skip, int take, Expression<Func<RoomRating, T>> selector) =>
        Db.RoomRatings.AsNoTracking().Where(x => x.RoomId == roomId).OrderByDescending(x => x.RatingDate)
            .ThenBy(x => x.RatingId).Skip(skip).Take(take).Select(selector).ToListAsync();
    public Task<RoomRating?> GetForTenantAsync(Guid roomId, Guid userId) =>
        Db.RoomRatings.SingleOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId);
}
