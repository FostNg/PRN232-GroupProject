using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IRepairRepository : IRepository<Repairing>
{
    Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Repairing, T>> selector);
    Task<Repairing?> GetWithRoomAsync(Guid id);
}
