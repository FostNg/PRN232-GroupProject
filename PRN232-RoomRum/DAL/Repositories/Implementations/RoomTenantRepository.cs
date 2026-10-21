using System.Linq.Expressions;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class RoomTenantRepository(RoomRumDbContext db) : Repository<RoomTenant>(db), IRoomTenantRepository
{
    public Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<RoomTenant, T>> selector) =>
        Db.RoomTenants.AsNoTracking().Where(x => x.UserId == userId || x.Room.UserId == userId)
            .OrderBy(x => x.RoomId).ThenBy(x => x.UserId).Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<bool> HasStatusAsync(Guid roomId, Guid userId, params string[] statuses) =>
        Db.RoomTenants.AnyAsync(x => x.RoomId == roomId && x.UserId == userId && statuses.Contains(x.Status));

    public Task<RoomTenant?> GetWithRoomAsync(Guid roomId, Guid userId) =>
        Db.RoomTenants.Include(x => x.Room).SingleOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId);

    public Task<T?> GetCurrentAsync<T>(Guid roomId, Guid userId, Expression<Func<RoomTenant, T>> selector) where T : class =>
        Db.RoomTenants.AsNoTracking().Where(x => x.RoomId == roomId && x.UserId == userId)
            .Select(selector).SingleOrDefaultAsync();

    public async Task<(List<T> Items, int Total)> GetHistoryAsync<T>(Guid roomId, Guid userId, int skip,
        int take, Expression<Func<RoomTenantHistory, T>> selector)
    {
        var query = Db.RoomTenantHistories.AsNoTracking().Where(x => x.RoomId == roomId && x.UserId == userId);
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.ArchivedAt).ThenBy(x => x.HistoryId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();
        return (items, total);
    }

    public async Task<bool> RestartAsync(RoomTenant previous, string nextStatus, DateTime now)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync();
        var changed = await Db.RoomTenants.Where(x => x.RoomId == previous.RoomId && x.UserId == previous.UserId &&
                x.Status == previous.Status && x.HiredAt == previous.HiredAt && x.LeftAt == previous.LeftAt)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus)
                .SetProperty(x => x.HiredAt, now).SetProperty(x => x.LeftAt, (DateTime?)null));
        if (changed != 1) return false;
        Db.RoomTenantHistories.Add(new RoomTenantHistory
        {
            HistoryId = Guid.NewGuid(), RoomId = previous.RoomId, UserId = previous.UserId,
            HiredAt = previous.HiredAt, LeftAt = previous.LeftAt, Status = previous.Status, ArchivedAt = now
        });
        await Db.SaveChangesAsync();
        await transaction.CommitAsync();
        // ExecuteUpdate bypasses tracking; avoid retaining the archived snapshot as current state.
        Db.Entry(previous).State = EntityState.Detached;
        return true;
    }

    public async Task<int> AcceptAsync(Guid roomId, Guid userId, string expectedStatus, string nextStatus, DateTime now)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync();
        var changed = await Db.RoomTenants.Where(x => x.RoomId == roomId && x.UserId == userId && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus).SetProperty(x => x.HiredAt, now));
        if (changed == 1)
            await Db.Rooms.Where(x => x.RoomId == roomId && x.Status == "Available")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Rented"));
        await transaction.CommitAsync();
        return changed;
    }

    public async Task<int> EndAsync(Guid roomId, Guid userId, string expectedStatus, string nextStatus, DateTime? leftAt)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var changed = await Db.RoomTenants.Where(x => x.RoomId == roomId && x.UserId == userId && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus).SetProperty(x => x.LeftAt, leftAt));
        if (changed == 1)
        {
            await Db.Rooms.Where(x => x.RoomId == roomId && x.Status == "Rented" &&
                    !Db.RoomTenants.Any(t => t.RoomId == roomId && t.Status == "Active"))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Available"));
        }
        await transaction.CommitAsync();
        return changed;
    }
}
