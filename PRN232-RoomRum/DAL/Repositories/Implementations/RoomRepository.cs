using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class RoomRepository(RoomRumDbContext db) : Repository<Room>(db), IRoomRepository
{
    private sealed record SearchCandidate(Guid RoomId, Guid UserId, string? Title,
        string? Address, string? Type, decimal? Price, decimal? Area, string? Status,
        string? ImageUrl, decimal? ElectricityFee, decimal? WaterFee,
        decimal? ServiceFee, double? Latitude, double? Longitude);

    private static readonly Expression<Func<Room, SearchCandidate>> SearchProjection = x => new(
        x.RoomId, x.UserId, x.Title, x.Address, x.Type, x.Price, x.Area, x.Status,
        x.RoomImages.OrderBy(i => i.RoomImageId).Select(i => i.ImageUrl).FirstOrDefault(),
        x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Latitude, x.Longitude);

    public async Task<RoomSearchResult> SearchAdvancedAsync(RoomSearchCriteria criteria)
    {
        var query = Db.Rooms.AsNoTracking().Where(x => x.Status == "Available" &&
            !x.RoomTenants.Any(t => t.Status == "Pending" || t.Status == "Active"));
        var term = criteria.Q?.Trim();
        if (!string.IsNullOrEmpty(term)) query = query.Where(x =>
            (x.Title != null && x.Title.Contains(term)) ||
            (x.Address != null && x.Address.Contains(term)));
        if (!string.IsNullOrWhiteSpace(criteria.Type)) query = query.Where(x => x.Type == criteria.Type);
        if (criteria.MinPrice.HasValue) query = query.Where(x => x.Price >= criteria.MinPrice);
        if (criteria.MaxPrice.HasValue) query = query.Where(x => x.Price <= criteria.MaxPrice);
        if (criteria.MinArea.HasValue) query = query.Where(x => x.Area >= criteria.MinArea);
        if (criteria.MaxArea.HasValue) query = query.Where(x => x.Area <= criteria.MaxArea);
        if (criteria.MaxDeposit.HasValue) query = query.Where(x => x.Deposit != null && x.Deposit <= criteria.MaxDeposit);
        if (criteria.MaxMoveInCost.HasValue) query = query.Where(x => x.Price != null && x.Deposit != null &&
            x.Price + x.Deposit <= criteria.MaxMoveInCost);
        if (criteria.MaxMonthlyCost.HasValue) query = query.Where(x =>
            x.Price != null && x.ServiceFee != null && x.WaterFee != null &&
            (criteria.MonthlyKwh == 0 || x.ElectricityFee != null) &&
            x.Price + x.ServiceFee + x.WaterFee * criteria.Occupants +
                (criteria.MonthlyKwh == 0 ? 0 : x.ElectricityFee * criteria.MonthlyKwh) <= criteria.MaxMonthlyCost);
        foreach (var id in criteria.AmenityIds)
            query = query.Where(x => x.Amenities.Any(a => a.AmenityId == id));

        if (criteria.MaxDistanceKm is double radius)
        {
            var latitudeSpan = radius / 110.6;
            var longitudeSpan = radius / (111.32 * Math.Max(0.01,
                Math.Cos(criteria.Latitude!.Value * Math.PI / 180)));
            var minLatitude = criteria.Latitude.Value - latitudeSpan;
            var maxLatitude = criteria.Latitude.Value + latitudeSpan;
            query = query.Where(x => x.Latitude != null && x.Longitude != null &&
                x.Latitude >= minLatitude && x.Latitude <= maxLatitude);
            if (criteria.Longitude!.Value - longitudeSpan >= -180 &&
                criteria.Longitude.Value + longitudeSpan <= 180)
            {
                var minLongitude = criteria.Longitude.Value - longitudeSpan;
                var maxLongitude = criteria.Longitude.Value + longitudeSpan;
                query = query.Where(x => x.Longitude >= minLongitude && x.Longitude <= maxLongitude);
            }
        }

        var hasOrigin = criteria.Latitude.HasValue && criteria.Longitude.HasValue;
        if (hasOrigin && (criteria.MaxDistanceKm.HasValue || criteria.Sort == "distance"))
        {
            var candidates = await query.Select(SearchProjection).ToListAsync();
            var located = candidates.Select(x => (Room: x,
                Distance: x.Latitude.HasValue && x.Longitude.HasValue
                    ? (double?)DistanceKm(criteria.Latitude!.Value, criteria.Longitude!.Value,
                        x.Latitude.Value, x.Longitude.Value) : null))
                .Where(x => !criteria.MaxDistanceKm.HasValue || x.Distance <= criteria.MaxDistanceKm.Value);
            var ordered = criteria.Sort switch
            {
                "priceAsc" => located.OrderBy(x => x.Room.Price ?? decimal.MaxValue).ThenBy(x => x.Room.RoomId),
                "priceDesc" => located.OrderByDescending(x => x.Room.Price).ThenBy(x => x.Room.RoomId),
                "areaDesc" => located.OrderByDescending(x => x.Room.Area).ThenBy(x => x.Room.RoomId),
                "monthlyCostAsc" => located.OrderBy(x => MonthlyCost(x.Room, criteria.Occupants,
                    criteria.MonthlyKwh) ?? decimal.MaxValue).ThenBy(x => x.Room.RoomId),
                "distance" => located.OrderBy(x => x.Distance ?? double.MaxValue).ThenBy(x => x.Room.RoomId),
                _ => located.OrderBy(x => x.Room.RoomId)
            };
            var total = ordered.Count();
            return new RoomSearchResult(ordered.Skip(criteria.Skip).Take(criteria.Take)
                .Select(x => ToSearchItem(x.Room, x.Distance)).ToList(), total);
        }

        var count = await query.CountAsync();
        var sorted = criteria.Sort switch
        {
            "priceAsc" => query.OrderBy(x => x.Price == null).ThenBy(x => x.Price).ThenBy(x => x.RoomId),
            "priceDesc" => query.OrderByDescending(x => x.Price).ThenBy(x => x.RoomId),
            "areaDesc" => query.OrderByDescending(x => x.Area).ThenBy(x => x.RoomId),
            "monthlyCostAsc" => query.OrderBy(x => x.Price == null || x.ServiceFee == null ||
                x.WaterFee == null || criteria.MonthlyKwh > 0 && x.ElectricityFee == null)
                .ThenBy(x => x.Price + x.ServiceFee + x.WaterFee * criteria.Occupants +
                    (x.ElectricityFee ?? 0) * criteria.MonthlyKwh).ThenBy(x => x.RoomId),
            _ => query.OrderBy(x => x.RoomId)
        };
        var page = await sorted.Skip(criteria.Skip).Take(criteria.Take).Select(SearchProjection).ToListAsync();
        return new RoomSearchResult(page.Select(x => ToSearchItem(x,
            hasOrigin && x.Latitude.HasValue && x.Longitude.HasValue
                ? DistanceKm(criteria.Latitude!.Value, criteria.Longitude!.Value,
                    x.Latitude.Value, x.Longitude.Value) : null)).ToList(), count);
    }

    private static RoomSearchItem ToSearchItem(SearchCandidate x, double? distance) => new(
        x.RoomId, x.UserId, x.Title, x.Address, x.Type, x.Price, x.Area, x.Status,
        x.ImageUrl, x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Latitude, x.Longitude,
        distance.HasValue ? Math.Round(distance.Value, 2) : null);

    private static decimal? MonthlyCost(SearchCandidate room, int occupants, int monthlyKwh) =>
        room.Price is decimal price && room.ServiceFee is decimal service &&
        room.WaterFee is decimal water && (monthlyKwh == 0 || room.ElectricityFee.HasValue)
            ? price + service + water * occupants + (room.ElectricityFee ?? 0) * monthlyKwh
            : null;

    private static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double radius = 6371.0088;
        var dLatitude = (latitude2 - latitude1) * Math.PI / 180;
        var dLongitude = (longitude2 - longitude1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLatitude / 2), 2) +
            Math.Cos(latitude1 * Math.PI / 180) * Math.Cos(latitude2 * Math.PI / 180) *
            Math.Pow(Math.Sin(dLongitude / 2), 2);
        return radius * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

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
        Db.Rooms.AsNoTracking().AsSplitQuery().Where(x => x.UserId == userId &&
            (x.Status == "Pending" || x.Status == "Available" || x.Status == "Rented"))
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
    public Task<Guid?> GetOwnerIdAsync(Guid roomId) => Db.Rooms.AsNoTracking()
        .Where(x => x.RoomId == roomId).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync();
    public Task<int> CountImagesAsync(Guid roomId) =>
        Db.RoomImages.CountAsync(x => x.RoomId == roomId);
    public Task<bool> ImageExistsAsync(Guid roomId, Guid imageId, string imageUrl) =>
        Db.RoomImages.AsNoTracking().AnyAsync(x => x.RoomId == roomId &&
            x.RoomImageId == imageId && x.ImageUrl == imageUrl);
    public Task<int> ReviewAsync(Guid id, string expectedStatus, string nextStatus) =>
        Db.Rooms.Where(x => x.RoomId == id && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus));
    public void AddImage(RoomImage image) => Db.RoomImages.Add(image);
    public void RemoveImage(RoomImage image) => Db.RoomImages.Remove(image);
}
