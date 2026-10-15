using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface ISharingService
{
    Task<ApiOutcome> List(Guid? roomId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, SharingRequest request);
    Task<ApiOutcome> Delete(Guid userId, bool isAdmin, Guid id);
}
public interface IRatingService
{
    Task<ApiOutcome> List(Guid roomId, int page, int pageSize);
    Task<ApiOutcome> Upsert(Guid userId, Guid roomId, RatingRequest request);
}
public interface IRoomTenantService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Add(Guid userId, Guid roomId, TenantRequest request);
    Task<ApiOutcome> Accept(Guid userId, Guid roomId);
    Task<ApiOutcome> End(Guid userId, Guid roomId, Guid tenantId);
    Task<ApiOutcome> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize);
}
public sealed class SharingService(NewRoomRumDbContext db) : ISharingService
{
    public async Task<ApiOutcome> List(Guid? roomId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Sharings.AsNoTracking()
        .Where(x => roomId == null || x.RoomId == roomId)
        .Where(x => x.Room.Status == "Available")
        .OrderBy(x => x.SharingId).Skip(skip).Take(pageSize)
        .Select(x => new SharingSummaryResponse(x.SharingId, x.UserId, x.RoomId,
            x.Title, x.Quantity, x.Gender, x.SharingFee, x.Introduction, x.Rule,
            x.Room.Title, x.User.Name))
        .ToListAsync());
    }
    public async Task<ApiOutcome> Create(Guid userId, SharingRequest request)
    {
        var allowed = await db.Rooms.AnyAsync(x => x.RoomId == request.RoomId && x.UserId == userId)
            || await db.RoomTenants.AnyAsync(x => x.RoomId == request.RoomId && x.UserId == userId && x.Status == "Active");
        if (!allowed) return ApiOutcome.Forbidden();
        if (request.Quantity is < 1 || request.SharingFee is < 0 or > 9999999999999999.99m ||
            request.SharingFee is decimal fee && decimal.Round(fee, 2) != fee ||
            request.Title?.Length > 255 || request.Gender?.Length > 20)
            return ApiOutcome.BadRequest("Invalid quantity, fee or database column limit exceeded.");
        var item = new Sharing { SharingId = Guid.NewGuid(), UserId = userId, RoomId = request.RoomId,
            Title = request.Title, Quantity = request.Quantity, Gender = request.Gender,
            SharingFee = request.SharingFee, Introduction = request.Introduction, Rule = request.Rule };
        db.Sharings.Add(item); await db.SaveChangesAsync();
        return ApiOutcome.Created(new SharingMutationResponse(item.SharingId));
    }
    public async Task<ApiOutcome> Delete(Guid userId, bool isAdmin, Guid id)
    {
        var item = await db.Sharings.FindAsync(id);
        if (item is null) return ApiOutcome.NotFound();
        if (item.UserId != userId && !isAdmin) return ApiOutcome.Forbidden();
        db.Sharings.Remove(item); await db.SaveChangesAsync(); return ApiOutcome.NoContent();
    }
}
public sealed class RatingService(NewRoomRumDbContext db) : IRatingService
{
    public async Task<ApiOutcome> List(Guid roomId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        if (!await db.Rooms.AnyAsync(x => x.RoomId == roomId && x.Status == "Available"))
            return ApiOutcome.NotFound();
        return ApiOutcome.Ok(await db.RoomRatings.AsNoTracking()
        .Where(x => x.RoomId == roomId).OrderByDescending(x => x.RatingDate)
        .ThenBy(x => x.RatingId).Skip(skip).Take(pageSize)
        .Select(x => new RatingSummaryResponse(x.RatingId, x.Rating, x.Comment,
            x.RatingDate, x.User.Name))
        .ToListAsync());
    }
    public async Task<ApiOutcome> Upsert(Guid userId, Guid roomId, RatingRequest request)
    {
        if (request.Rating is < 1 or > 5) return ApiOutcome.BadRequest("Rating must be 1-5.");
        if (!await db.RoomTenants.AnyAsync(x => x.RoomId == roomId && x.UserId == userId &&
            (x.Status == "Active" || x.Status == "Ended")))
            return ApiOutcome.Forbidden();
        var rating = await db.RoomRatings.SingleOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId);
        if (rating is null)
        {
            rating = new RoomRating { RatingId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
            db.RoomRatings.Add(rating);
        }
        rating.Rating = request.Rating; rating.Comment = request.Comment; rating.RatingDate = DateTime.Now;
        await db.SaveChangesAsync(); return ApiOutcome.Ok(new RatingMutationResponse(rating.RatingId));
    }
}
public sealed class RoomTenantService(NewRoomRumDbContext db) : IRoomTenantService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.RoomTenants.AsNoTracking()
        .Where(x => x.UserId == userId || x.Room.UserId == userId)
        .OrderBy(x => x.RoomId).ThenBy(x => x.UserId).Skip(skip).Take(pageSize)
        .Select(x => new RoomTenantSummaryResponse(x.RoomId, x.UserId, x.Status,
            x.HiredAt, x.LeftAt, x.Room.Title))
        .ToListAsync());
    }
    public async Task<ApiOutcome> Add(Guid userId, Guid roomId, TenantRequest request)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId) return ApiOutcome.Forbidden();
        if (room.UserId == request.UserId || !await db.Users.AnyAsync(x => x.UserId == request.UserId))
            return ApiOutcome.BadRequest("Invalid tenant.");
        var existing = await db.RoomTenants.FindAsync(roomId, request.UserId);
        if (existing is not null)
        {
            if (existing.Status is not ("Ended" or "Cancelled"))
                return ApiOutcome.Conflict("This user already has a pending or active tenancy for the room.");
            var now = DateTime.Now;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var changed = await db.RoomTenants
                .Where(x => x.RoomId == roomId && x.UserId == request.UserId &&
                    x.Status == existing.Status && x.HiredAt == existing.HiredAt &&
                    x.LeftAt == existing.LeftAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Pending")
                    .SetProperty(x => x.HiredAt, now)
                    .SetProperty(x => x.LeftAt, (DateTime?)null));
            if (changed != 1)
                return ApiOutcome.Conflict("Tenancy status changed. Reload it and try again.");
            db.RoomTenantHistories.Add(new RoomTenantHistory
            {
                HistoryId = Guid.NewGuid(), RoomId = roomId, UserId = request.UserId,
                HiredAt = existing.HiredAt, LeftAt = existing.LeftAt,
                Status = existing.Status, ArchivedAt = now
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ApiOutcome.Created(new RoomTenantCreatedResponse(roomId, request.UserId));
        }
        db.RoomTenants.Add(new RoomTenant { RoomId = roomId, UserId = request.UserId, Status = "Pending" });
        await db.SaveChangesAsync(); return ApiOutcome.Created(new RoomTenantCreatedResponse(roomId, request.UserId));
    }
    public async Task<ApiOutcome> Accept(Guid userId, Guid roomId)
    {
        var now = DateTime.Now;
        var changed = await db.RoomTenants
            .Where(x => x.RoomId == roomId && x.UserId == userId && x.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Active")
                .SetProperty(x => x.HiredAt, now));
        if (changed == 0) return ApiOutcome.Conflict("No pending tenancy invitation for this room.");
        return ApiOutcome.Ok(new RoomTenantCreatedResponse(roomId, userId));
    }
    public async Task<ApiOutcome> End(Guid userId, Guid roomId, Guid tenantId)
    {
        var item = await db.RoomTenants.Include(x => x.Room).SingleOrDefaultAsync(x => x.RoomId == roomId && x.UserId == tenantId);
        if (item is null) return ApiOutcome.NotFound();
        if (item.Room.UserId != userId && tenantId != userId) return ApiOutcome.Forbidden();
        if (item.Status is not ("Active" or "Pending"))
            return ApiOutcome.Conflict("Tenancy is neither active nor pending.");
        var nextStatus = item.Status == "Pending" ? "Cancelled" : "Ended";
        var changed = await db.RoomTenants
            .Where(x => x.RoomId == roomId && x.UserId == tenantId && x.Status == item.Status)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, nextStatus)
                .SetProperty(x => x.LeftAt, nextStatus == "Ended" ? DateTime.Now : (DateTime?)null));
        if (changed == 0) return ApiOutcome.Conflict("Tenancy status changed. Reload it and try again.");
        return ApiOutcome.Ok(new RoomTenantEndedResponse(item.RoomId, item.UserId, nextStatus));
    }

    public async Task<ApiOutcome> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        var current = await db.RoomTenants.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.UserId == tenantId)
            .Select(x => new { x.Status, x.HiredAt, x.LeftAt, OwnerId = x.Room.UserId })
            .SingleOrDefaultAsync();
        if (current is null) return ApiOutcome.NotFound();
        if (userId != tenantId && userId != current.OwnerId) return ApiOutcome.Forbidden();

        var query = db.RoomTenantHistories.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.UserId == tenantId);
        var archivedCount = await query.CountAsync();
        var offset = (page - 1) * pageSize;
        var items = await query.OrderBy(x => x.ArchivedAt).ThenBy(x => x.HistoryId)
            .Skip(offset).Take(pageSize)
            .Select(x => new RoomTenantPeriodResponse(x.HistoryId, x.Status,
                x.HiredAt, x.LeftAt, x.ArchivedAt, false))
            .ToListAsync();
        if (offset <= archivedCount && (long)offset + pageSize > archivedCount)
            items.Add(new RoomTenantPeriodResponse(null, current.Status,
                current.HiredAt, current.LeftAt, null, true));
        return ApiOutcome.Ok(new RoomTenantHistoryPageResponse(items, archivedCount + 1, page, pageSize));
    }
}
