using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IRoomTenantRepository : IRepository<RoomTenant>
{
    Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<RoomTenant, T>> selector);
    Task<bool> HasStatusAsync(Guid roomId, Guid userId, params string[] statuses);
    Task<RoomTenant?> GetWithRoomAsync(Guid roomId, Guid userId);
    Task<T?> GetCurrentAsync<T>(Guid roomId, Guid userId, Expression<Func<RoomTenant, T>> selector) where T : class;
    Task<(List<T> Items, int Total)> GetHistoryAsync<T>(Guid roomId, Guid userId, int skip, int take,
        Expression<Func<RoomTenantHistory, T>> selector);
    Task<bool> RestartAsync(RoomTenant previous, string nextStatus, DateTime now);
    Task<int> AcceptAsync(Guid roomId, Guid userId, string expectedStatus, string nextStatus, DateTime now);
    Task<int> EndAsync(Guid roomId, Guid userId, string expectedStatus, string nextStatus, DateTime? leftAt);
}
