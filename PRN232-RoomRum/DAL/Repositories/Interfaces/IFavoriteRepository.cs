using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IFavoriteRepository
{
    Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Favorite, T>> selector);
    Task<bool> AddIfMissingAsync(Favorite favorite);
    Task RemoveAsync(Guid userId, Guid roomId);
}
