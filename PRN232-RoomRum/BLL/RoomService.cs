using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IRoomService
{
    Task<ApiOutcome> Search(RoomSearchRequest request);
    Task<ApiOutcome> Detail(Guid id);
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, RoomRequest request);
    Task<ApiOutcome> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request);
    Task<ApiOutcome> Hide(Guid userId, bool isAdmin, Guid id);
    Task<ApiOutcome> Amenities(int page, int pageSize);
}
public sealed class RoomService(NewRoomRumDbContext db) : IRoomService
{
    public async Task<ApiOutcome> Search(RoomSearchRequest request)
    {
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 100 ||
            (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        if (!ValidAmount(request.MinPrice) || !ValidAmount(request.MaxPrice) ||
            !ValidAmount(request.MinArea, 99999999.99m) || !ValidAmount(request.MaxArea, 99999999.99m) ||
            !ValidAmount(request.MaxDeposit) || !ValidAmount(request.MaxMoveInCost) ||
            !ValidAmount(request.MaxMonthlyCost) || request.MinPrice > request.MaxPrice ||
            request.MinArea > request.MaxArea || request.Occupants is < 1 or > 10 ||
            request.MonthlyKwh is < 0 or > 2000 ||
            request.Q?.Length > 200 || request.Type?.Length > 50)
            return ApiOutcome.BadRequest("Invalid search range or estimated cost inputs.");
        var hasOrigin = request.Latitude.HasValue && request.Longitude.HasValue;
        if (request.Latitude.HasValue != request.Longitude.HasValue ||
            request.Latitude is double lat && (!double.IsFinite(lat) || lat is < -90 or > 90) ||
            request.Longitude is double lon && (!double.IsFinite(lon) || lon is < -180 or > 180) ||
            request.MaxDistanceKm.HasValue && (!hasOrigin || !double.IsFinite(request.MaxDistanceKm.Value) ||
                request.MaxDistanceKm is <= 0 or > 100) ||
            request.Sort is not (null or "relevance" or "priceAsc" or "priceDesc" or "areaDesc" or "monthlyCostAsc" or "distance") ||
            request.Sort == "distance" && !hasOrigin)
            return ApiOutcome.BadRequest("Invalid location, distance or sort option.");
        var query = db.Rooms.AsNoTracking().Where(x => x.Status == "Available");
        var q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q)) query = query.Where(x =>
            (x.Title != null && x.Title.Contains(q)) || (x.Address != null && x.Address.Contains(q)));
        if (!string.IsNullOrWhiteSpace(request.Type)) query = query.Where(x => x.Type == request.Type);
        if (request.MinPrice.HasValue) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice.HasValue) query = query.Where(x => x.Price <= request.MaxPrice);
        if (request.MinArea.HasValue) query = query.Where(x => x.Area >= request.MinArea);
        if (request.MaxArea.HasValue) query = query.Where(x => x.Area <= request.MaxArea);
        if (request.MaxDeposit.HasValue) query = query.Where(x => x.Deposit != null && x.Deposit <= request.MaxDeposit);
        if (request.MaxMoveInCost.HasValue) query = query.Where(x => x.Price != null && x.Deposit != null &&
            x.Price + x.Deposit <= request.MaxMoveInCost);
        if (request.MaxMonthlyCost.HasValue) query = query.Where(x => x.Price != null && x.ServiceFee != null &&
            x.WaterFee != null && (request.MonthlyKwh == 0 || x.ElectricityFee != null) &&
            x.Price + x.ServiceFee + x.WaterFee * request.Occupants +
                (request.MonthlyKwh == 0 ? 0 : x.ElectricityFee * request.MonthlyKwh) <= request.MaxMonthlyCost);
        var amenityIds = (request.AmenityIds ?? []).Concat(request.AmenityId is Guid oldId ? [oldId] : [])
            .Distinct().ToList();
        foreach (var id in amenityIds)
            query = query.Where(x => x.Amenities.Any(a => a.AmenityId == id));

        if (request.MaxDistanceKm is double radius)
        {
            var latitudeSpan = radius / 110.6;
            var longitudeSpan = radius / (111.32 * Math.Max(0.01, Math.Cos(request.Latitude!.Value * Math.PI / 180)));
            var minLatitude = request.Latitude.Value - latitudeSpan;
            var maxLatitude = request.Latitude.Value + latitudeSpan;
            query = query.Where(x => x.Latitude != null && x.Longitude != null &&
                x.Latitude >= minLatitude && x.Latitude <= maxLatitude);
            if (request.Longitude!.Value - longitudeSpan >= -180 && request.Longitude.Value + longitudeSpan <= 180)
            {
                var minLongitude = request.Longitude.Value - longitudeSpan;
                var maxLongitude = request.Longitude.Value + longitudeSpan;
                query = query.Where(x => x.Longitude >= minLongitude && x.Longitude <= maxLongitude);
            }
        }

        var projected = query.Select(x => new RoomSearchCandidate(x.RoomId, x.UserId,
            x.Title, x.Address, x.Type, x.Price, x.Area, x.Status,
            x.RoomImages.OrderBy(i => i.RoomImageId).Select(i => i.ImageUrl).FirstOrDefault(),
            x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Latitude, x.Longitude));
        if (hasOrigin && (request.MaxDistanceKm.HasValue || request.Sort == "distance"))
        {
            var originLat = request.Latitude!.Value;
            var originLon = request.Longitude!.Value;
            var candidates = await projected.ToListAsync();
            var located = candidates.Select(x => (Room: x,
                Distance: x.Latitude.HasValue && x.Longitude.HasValue
                    ? (double?)DistanceKm(originLat, originLon, x.Latitude.Value, x.Longitude.Value) : null))
                .Where(x => !request.MaxDistanceKm.HasValue || x.Distance <= request.MaxDistanceKm.Value);
            var ordered = request.Sort switch
            {
                "priceAsc" => located.OrderBy(x => x.Room.Price ?? decimal.MaxValue).ThenBy(x => x.Room.RoomId),
                "priceDesc" => located.OrderByDescending(x => x.Room.Price).ThenBy(x => x.Room.RoomId),
                "areaDesc" => located.OrderByDescending(x => x.Room.Area).ThenBy(x => x.Room.RoomId),
                "monthlyCostAsc" => located.OrderBy(x => MonthlyCost(x.Room, request.Occupants, request.MonthlyKwh) ?? decimal.MaxValue).ThenBy(x => x.Room.RoomId),
                "distance" => located.OrderBy(x => x.Distance ?? double.MaxValue).ThenBy(x => x.Room.RoomId),
                _ => located.OrderBy(x => x.Room.RoomId)
            };
            var total = ordered.Count();
            var items = ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                .Select(x => ToSummary(x.Room, request.Occupants, request.MonthlyKwh, x.Distance)).ToList();
            return ApiOutcome.Ok(new RoomSearchResponse(items, total, request.Page, request.PageSize));
        }

        var count = await query.CountAsync();
        var sorted = request.Sort switch
        {
            "priceAsc" => query.OrderBy(x => x.Price == null).ThenBy(x => x.Price).ThenBy(x => x.RoomId),
            "priceDesc" => query.OrderByDescending(x => x.Price).ThenBy(x => x.RoomId),
            "areaDesc" => query.OrderByDescending(x => x.Area).ThenBy(x => x.RoomId),
            "monthlyCostAsc" => query.OrderBy(x => x.Price == null || x.ServiceFee == null ||
                x.WaterFee == null || request.MonthlyKwh > 0 && x.ElectricityFee == null)
                .ThenBy(x => x.Price + x.ServiceFee + x.WaterFee * request.Occupants +
                    (x.ElectricityFee ?? 0) * request.MonthlyKwh).ThenBy(x => x.RoomId),
            _ => query.OrderBy(x => x.RoomId)
        };
        var pageItems = await sorted.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new RoomSearchCandidate(x.RoomId, x.UserId,
                x.Title, x.Address, x.Type, x.Price, x.Area, x.Status,
                x.RoomImages.OrderBy(i => i.RoomImageId).Select(i => i.ImageUrl).FirstOrDefault(),
                x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Latitude, x.Longitude)).ToListAsync();
        return ApiOutcome.Ok(new RoomSearchResponse(pageItems.Select(x =>
            ToSummary(x, request.Occupants, request.MonthlyKwh,
                hasOrigin && x.Latitude.HasValue && x.Longitude.HasValue
                    ? DistanceKm(request.Latitude!.Value, request.Longitude!.Value, x.Latitude.Value, x.Longitude.Value)
                    : null)).ToList(),
            count, request.Page, request.PageSize));
    }

    

    private static decimal? MonthlyCost(RoomSearchCandidate room, int occupants, int monthlyKwh) =>
        room.Price is decimal price && room.ServiceFee is decimal service &&
        room.WaterFee is decimal water && (monthlyKwh == 0 || room.ElectricityFee.HasValue)
            ? price + service + water * occupants + (room.ElectricityFee ?? 0) * monthlyKwh
            : null;

    private sealed record RoomSearchCandidate(Guid RoomId, Guid UserId, string? Title,
        string? Address, string? Type, decimal? Price, decimal? Area, string? Status,
        string? ImageUrl, decimal? ElectricityFee, decimal? WaterFee,
        decimal? ServiceFee, double? Latitude, double? Longitude);

    private static RoomSummaryResponse ToSummary(RoomSearchCandidate x, int occupants,
        int monthlyKwh, double? distance) => new(x.RoomId, x.UserId, x.Title, x.Address,
            x.Type, x.Price, x.Area, x.Status, x.ImageUrl,
            MonthlyCost(x, occupants, monthlyKwh), distance.HasValue ? Math.Round(distance.Value, 2) : null,
            x.Latitude, x.Longitude);

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
    public async Task<ApiOutcome> Detail(Guid id)
    {
        var room = await db.Rooms.AsNoTracking().AsSplitQuery().Where(x => x.RoomId == id && x.Status == "Available")
            .Select(x => new { x.RoomId, x.UserId, x.Title, x.Type, x.Status, x.Address, x.Area,
                x.Latitude, x.Longitude,
                x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Deposit, x.Price, x.Description, x.Rule,
                owner = new { x.User.UserId, x.User.Name, x.User.Phone },
                amenities = x.Amenities.Select(a => new { a.AmenityId, a.AmenityName }),
                images = x.RoomImages.Select(i => new { i.RoomImageId, i.ImageUrl }),
                averageRating = x.RoomRatings.Select(r => (double?)r.Rating).Average(),
                ratingCount = x.RoomRatings.Count }).SingleOrDefaultAsync();
        if (room is null) return ApiOutcome.NotFound();
        return ApiOutcome.Ok(new RoomDetailResponse(room.RoomId, room.UserId, room.Title,
            room.Type, room.Status, room.Address, room.Area, room.ElectricityFee,
            room.WaterFee, room.ServiceFee, room.Deposit, room.Price, room.Description,
            room.Rule, new RoomOwnerResponse(room.owner.UserId, room.owner.Name, room.owner.Phone),
            room.amenities.Select(a => new RoomAmenityResponse(a.AmenityId, a.AmenityName)).ToList(),
            room.images.Select(i => new RoomImageResponse(i.RoomImageId, i.ImageUrl)).ToList(),
            room.averageRating, room.ratingCount, room.Latitude, room.Longitude));
    }
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Rooms.AsNoTracking().AsSplitQuery()
        .Where(x => x.UserId == userId).OrderBy(x => x.Title).ThenBy(x => x.RoomId)
        .Skip(skip).Take(pageSize)
        .Select(x => new RoomMineResponse(x.RoomId, x.UserId, x.Status, x.Type,
            x.Title, x.Address, x.Area, x.ElectricityFee, x.WaterFee, x.ServiceFee,
            x.Deposit, x.Price, x.Description, x.Rule,
            x.Amenities.OrderBy(a => a.AmenityName)
                .Select(a => new RoomAmenityResponse(a.AmenityId, a.AmenityName)).ToList(),
            x.RoomImages.OrderBy(i => i.RoomImageId)
                .Select(i => new RoomImageResponse(i.RoomImageId, i.ImageUrl)).ToList(),
            x.Latitude, x.Longitude)).ToListAsync());
    }
    public async Task<ApiOutcome> Create(Guid userId, RoomRequest request)
    {
        if (!ValidRoom(request)) return ApiOutcome.BadRequest("Invalid room values or database column limit exceeded.");
        var room = new Room { RoomId = Guid.NewGuid(), UserId = userId };
        Apply(room, request);
        if (!await AddRelations(room, request)) return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return ApiOutcome.Created(new RoomMutationResponse(room.RoomId));
    }
    public async Task<ApiOutcome> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request)
    {
        var room = await db.Rooms.Include(x => x.Amenities).Include(x => x.RoomImages).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.RoomId == id);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId && !isAdmin) return ApiOutcome.Forbidden();
        if (!ValidRoom(request)) return ApiOutcome.BadRequest("Invalid room values or database column limit exceeded.");
        var amenityIds = request.AmenityIds?.Distinct().ToList() ?? [];
        var amenities = await db.Amenities.Where(x => amenityIds.Contains(x.AmenityId)).ToListAsync();
        var imageUrls = NormalizeImageUrls(request.ImageUrls);
        if (amenities.Count != amenityIds.Count || imageUrls is null)
            return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");

        var previousLatitude = room.Latitude;
        var previousLongitude = room.Longitude;
        Apply(room, request);
        if (!request.ClearCoordinates && !request.Latitude.HasValue)
        {
            room.Latitude = previousLatitude;
            room.Longitude = previousLongitude;
        }
        foreach (var amenity in room.Amenities.Where(x => !amenityIds.Contains(x.AmenityId)).ToList())
            room.Amenities.Remove(amenity);
        foreach (var amenity in amenities.Where(x => room.Amenities.All(a => a.AmenityId != x.AmenityId)))
            room.Amenities.Add(amenity);
        var oldImages = room.RoomImages.ToList();
        var reusable = oldImages.GroupBy(x => x.ImageUrl)
            .ToDictionary(x => x.Key, x => new Queue<RoomImage>(x));
        var preservedIds = new HashSet<Guid>();
        foreach (var url in imageUrls)
        {
            if (reusable.TryGetValue(url, out var matches) && matches.Count > 0)
                preservedIds.Add(matches.Dequeue().RoomImageId);
            else
                db.RoomImages.Add(new RoomImage { RoomImageId = Guid.NewGuid(), RoomId = id, ImageUrl = url });
        }
        foreach (var image in oldImages.Where(x => !preservedIds.Contains(x.RoomImageId)))
            db.RoomImages.Remove(image);
        await db.SaveChangesAsync();
        return ApiOutcome.Ok(new RoomMutationResponse(room.RoomId));
    }
    public async Task<ApiOutcome> Hide(Guid userId, bool isAdmin, Guid id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId && !isAdmin) return ApiOutcome.Forbidden();
        room.Status = "Hidden";
        await db.SaveChangesAsync();
        return ApiOutcome.NoContent();
    }
    public async Task<ApiOutcome> Amenities(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Amenities.AsNoTracking()
        .OrderBy(x => x.AmenityName).ThenBy(x => x.AmenityId)
        .Skip(skip).Take(pageSize)
        .Select(x => new RoomAmenityResponse(x.AmenityId, x.AmenityName)).ToListAsync());
    }

    private static void Apply(Room room, RoomRequest x)
    {
        room.Type = x.Type; room.Title = x.Title?.Trim(); room.Address = x.Address;
        room.Area = x.Area; room.ElectricityFee = x.ElectricityFee; room.WaterFee = x.WaterFee;
        room.ServiceFee = x.ServiceFee; room.Deposit = x.Deposit; room.Price = x.Price;
        room.Description = x.Description; room.Rule = x.Rule;
        room.Status = x.Status ?? room.Status ?? "Available";
        room.Latitude = x.Latitude; room.Longitude = x.Longitude;
    }

    private static bool ValidRoom(RoomRequest x) =>
        !string.IsNullOrWhiteSpace(x.Title) && x.Title.Trim().Length <= 255 &&
        (x.Type is null || x.Type.Length <= 50) &&
        (x.Status is null or "Available" or "Rented" or "Draft" or "Hidden" or "Deleted") &&
        ValidAmount(x.Area, 99999999.99m) &&
        ValidAmount(x.Price) && ValidAmount(x.ElectricityFee) &&
        ValidAmount(x.WaterFee) && ValidAmount(x.ServiceFee) &&
        ValidAmount(x.Deposit) && ValidCoordinates(x.Latitude, x.Longitude) &&
        (!x.ClearCoordinates || !x.Latitude.HasValue && !x.Longitude.HasValue);

    private static bool ValidCoordinates(double? latitude, double? longitude) =>
        latitude.HasValue == longitude.HasValue &&
        (latitude is null || double.IsFinite(latitude.Value) && latitude is >= -90 and <= 90 &&
            double.IsFinite(longitude!.Value) && longitude is >= -180 and <= 180);

    private static bool ValidAmount(decimal? value, decimal max = 9999999999999999.99m) =>
        value is null || value >= 0 && value <= max && decimal.Round(value.Value, 2) == value.Value;

    private async Task<bool> AddRelations(Room room, RoomRequest request)
    {
        var ids = request.AmenityIds?.Distinct().ToList() ?? [];
        var amenities = await db.Amenities.Where(x => ids.Contains(x.AmenityId)).ToListAsync();
        if (amenities.Count != ids.Count) return false;
        foreach (var amenity in amenities) room.Amenities.Add(amenity);
        var imageUrls = NormalizeImageUrls(request.ImageUrls);
        if (imageUrls is null) return false;
        foreach (var url in imageUrls)
        {
            room.RoomImages.Add(new RoomImage { RoomImageId = Guid.NewGuid(), ImageUrl = url });
        }
        return true;
    }

    private static List<string>? NormalizeImageUrls(List<string>? input)
    {
        var result = new List<string>();
        foreach (var raw in input ?? [])
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https")) return null;
            var normalized = new UriBuilder(uri) { Host = uri.IdnHost }.Uri.AbsoluteUri;
            if (normalized.Any(c => c > 127)) return null;
            result.Add(normalized);
        }
        return result;
    }
}
