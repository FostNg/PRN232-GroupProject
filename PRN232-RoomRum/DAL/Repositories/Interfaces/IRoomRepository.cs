using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IRoomRepository : IRepository<Room>
{
    Task<(List<T> Items, int Total)> SearchAsync<T>(string? q, string? type, decimal? minPrice,
        decimal? maxPrice, Guid? amenityId, int skip, int take, Expression<Func<Room, T>> selector);
    Task<T?> GetPublicDetailAsync<T>(Guid id, Guid? viewerId, Expression<Func<Room, T>> selector) where T : class;
    Task<List<T>> ListOwnedAsync<T>(Guid userId, int skip, int take, Expression<Func<Room, T>> selector);
    Task<List<T>> ListPendingAsync<T>(int skip, int take, Expression<Func<Room, T>> selector);
    Task<Room?> GetWithRelationsAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> IsPublicAsync(Guid id);
    Task<bool> IsBookableAsync(Guid id);
    Task<bool> IsOwnerAsync(Guid roomId, Guid userId);
    Task<int> ReviewAsync(Guid id, string expectedStatus, string nextStatus);
    void AddImage(RoomImage image);
    void RemoveImage(RoomImage image);
}
