using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IAmenityRepository
{
    Task<List<Amenity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
    Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<Amenity, T>> selector);
    Task<(Amenity Item, bool Created)> GetOrCreateAsync(string name);
}
