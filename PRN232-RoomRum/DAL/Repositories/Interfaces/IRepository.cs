namespace RoomRum.DAL.Repositories.Interfaces;

/// <summary>Tracked writes share the request-scoped context; SaveChangesAsync commits the unit of work.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    ValueTask<TEntity?> FindAsync(params object[] keys);
    void Add(TEntity entity);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync();
}
