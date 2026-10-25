using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IReportRepository : IRepository<Report>
{
    Task<List<T>> ListAsync<T>(Guid userId, bool isAdmin, int skip, int take, Expression<Func<Report, T>> selector);
    Task<List<T>> ListPendingAsync<T>(int skip, int take, Expression<Func<Report, T>> selector);
    Task<bool> ExistsAsync(Guid id);
    Task<int> ReviewAsync(Guid id, string nextStatus);
}
