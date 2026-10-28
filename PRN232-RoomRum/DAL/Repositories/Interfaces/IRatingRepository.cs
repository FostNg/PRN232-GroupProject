using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IRatingRepository : IRepository<RoomRating>
{
    Task<List<T>> ListAsync<T>(Guid roomId, int skip, int take, Expression<Func<RoomRating, T>> selector);
    Task<RoomRating?> GetForTenantAsync(Guid roomId, Guid userId);
}
