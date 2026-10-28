using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface ISharingRepository : IRepository<Sharing>
{
    Task<List<T>> ListAsync<T>(Guid? roomId, int skip, int take, Expression<Func<Sharing, T>> selector);
}
