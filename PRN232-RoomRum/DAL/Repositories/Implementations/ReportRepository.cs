using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class ReportRepository(RoomRumDbContext db) : Repository<Report>(db), IReportRepository
{
    public Task<List<T>> ListAsync<T>(Guid userId, bool isAdmin, int skip, int take, Expression<Func<Report, T>> selector) =>
        Db.Reports.AsNoTracking().Where(x => x.UserId == userId || isAdmin)
            .OrderByDescending(x => x.ReportDate).ThenBy(x => x.ReportId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<List<T>> ListPendingAsync<T>(int skip, int take, Expression<Func<Report, T>> selector) =>
        Db.Reports.AsNoTracking().Where(x => x.Status == "Pending")
            .OrderBy(x => x.ReportDate).ThenBy(x => x.ReportId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<bool> ExistsAsync(Guid id) => Db.Reports.AnyAsync(x => x.ReportId == id);

    public Task<int> ReviewAsync(Guid id, string nextStatus) =>
        Db.Reports.Where(x => x.ReportId == id && x.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus));
}
