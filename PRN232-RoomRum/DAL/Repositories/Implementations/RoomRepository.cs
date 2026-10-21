using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class RoomRepository(RoomRumDbContext db) : Repository<Room>(db), IRoomRepository
{
    public async Task<(List<T> Items, int Total)> SearchAsync<T>(string? q, string? type,
        decimal? minPrice, decimal? maxPrice, Guid? amenityId, int skip, int take,
        Expression<Func<Room, T>> selector)
    {
        var query = Db.Rooms.AsNoTracking().Where(x => x.Status == "Available" &&
            !x.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active"));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x =>
            (x.Title != null && x.Title.Contains(q)) || (x.Address != null && x.Address.Contains(q)));
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (minPrice.HasValue) query = query.Where(x => x.Price >= minPrice);
        if (maxPrice.HasValue) query = query.Where(x => x.Price <= maxPrice);
        if (amenityId.HasValue) query = query.Where(x => x.Amenities.Any(a => a.AmenityId == amenityId));
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.RoomId).Skip(skip).Take(take).Select(selector).ToListAsync();
        return (items, total);
    }

    public Task<T?> GetPublicDetailAsync<T>(Guid id, Guid? viewerId, Expression<Func<Room, T>> selector) where T : class =>
        Db.Rooms.AsNoTracking().AsSplitQuery()
            .Where(x => x.RoomId == id && (x.Status == "Rented" ||
                x.Status == "Available" && (
                    !x.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active") ||
                    viewerId.HasValue && (x.UserId == viewerId.Value ||
                        x.RoomTenants.Any(t => t.Status == "Pending" && t.UserId == viewerId.Value)))))
            .Select(selector).SingleOrDefaultAsync();

    public Task<List<T>> ListOwnedAsync<T>(Guid userId, int skip, int take, Expression<Func<Room, T>> selector) =>
        Db.Rooms.AsNoTracking().Where(x => x.UserId == userId &&
            (x.Status == "Available" || x.Status == "Rented"))
            .OrderBy(x => x.Title).ThenBy(x => x.RoomId).Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<List<T>> ListPendingAsync<T>(int skip, int take, Expression<Func<Room, T>> selector) =>
        Db.Rooms.AsNoTracking().Where(x => x.Status == "Pending")
            .OrderBy(x => x.RoomId).Skip(skip).Take(take).Select(selector).ToListAsync();

    public Task<Room?> GetWithRelationsAsync(Guid id) =>
        Db.Rooms.Include(x => x.Amenities).Include(x => x.RoomImages).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.RoomId == id);

    public Task<bool> ExistsAsync(Guid id) => Db.Rooms.AnyAsync(x => x.RoomId == id);
    public Task<bool> IsPublicAsync(Guid id) => Db.Rooms.AnyAsync(x =>
        x.RoomId == id && (x.Status == "Rented" || x.Status == "Available" &&
            !x.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active")));
    public Task<bool> IsBookableAsync(Guid id) => Db.Rooms.AnyAsync(x =>
        x.RoomId == id && x.Status == "Available" &&
        !x.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active"));
    public Task<bool> IsOwnerAsync(Guid roomId, Guid userId) =>
        Db.Rooms.AnyAsync(x => x.RoomId == roomId && x.UserId == userId);
    public Task<int> ReviewAsync(Guid id, string expectedStatus, string nextStatus) =>
        Db.Rooms.Where(x => x.RoomId == id && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus));
    public void AddImage(RoomImage image) => Db.RoomImages.Add(image);
    public void RemoveImage(RoomImage image) => Db.RoomImages.Remove(image);
}
