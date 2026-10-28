using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public abstract class Repository<TEntity>(RoomRumDbContext db) : IRepository<TEntity> where TEntity : class
{
    protected RoomRumDbContext Db { get; } = db;
    public ValueTask<TEntity?> FindAsync(params object[] keys) => Db.Set<TEntity>().FindAsync(keys);
    public void Add(TEntity entity) => Db.Set<TEntity>().Add(entity);
    public void Remove(TEntity entity) => Db.Set<TEntity>().Remove(entity);
    public Task<int> SaveChangesAsync() => Db.SaveChangesAsync();
}
