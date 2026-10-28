using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class AmenityRepository(RoomRumDbContext db) : IAmenityRepository
{
    public Task<List<Amenity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids) =>
        db.Amenities.Where(x => ids.Contains(x.AmenityId)).ToListAsync();
    public Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<Amenity, T>> selector) =>
        db.Amenities.AsNoTracking().OrderBy(x => x.AmenityName).ThenBy(x => x.AmenityId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();

    public async Task<(Amenity Item, bool Created)> GetOrCreateAsync(string name)
    {
        var existing = await db.Amenities.SingleOrDefaultAsync(x => x.AmenityName == name);
        if (existing is not null) return (existing, false);
        var item = new Amenity { AmenityId = Guid.NewGuid(), AmenityName = name };
        db.Amenities.Add(item);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(item).State = EntityState.Detached;
            existing = await db.Amenities.AsNoTracking().SingleOrDefaultAsync(x => x.AmenityName == name);
            if (existing is not null) return (existing, false);
            throw;
        }
        return (item, true);
    }
}
