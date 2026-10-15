using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IRoomService
{
    Task<ApiOutcome> Search(string? q, string? type, decimal? minPrice, decimal? maxPrice, Guid? amenityId, int page, int pageSize);
    Task<ApiOutcome> Detail(Guid id);
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Create(Guid userId, RoomRequest request);
    Task<ApiOutcome> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request);
    Task<ApiOutcome> Hide(Guid userId, bool isAdmin, Guid id);
    Task<ApiOutcome> Amenities(int page, int pageSize);
}
public sealed class RoomService(NewRoomRumDbContext db) : IRoomService
{
    public async Task<ApiOutcome> Search(string? q, string? type,
        decimal? minPrice, decimal? maxPrice,
        Guid? amenityId, int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        if (!ValidAmount(minPrice) || !ValidAmount(maxPrice) || minPrice > maxPrice)
            return ApiOutcome.BadRequest("Invalid price range or decimal precision.");
        var query = db.Rooms.AsNoTracking().Where(x => x.Status == "Available");
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => (x.Title != null && x.Title.Contains(q)) || (x.Address != null && x.Address.Contains(q)));
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (minPrice.HasValue) query = query.Where(x => x.Price >= minPrice);
        if (maxPrice.HasValue) query = query.Where(x => x.Price <= maxPrice);
        if (amenityId.HasValue) query = query.Where(x => x.Amenities.Any(a => a.AmenityId == amenityId));
        var total = await query.CountAsync();
        var rooms = await query.OrderBy(x => x.RoomId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new RoomSummaryResponse(x.RoomId, x.UserId, x.Title, x.Address,
                x.Type, x.Price, x.Area, x.Status,
                x.RoomImages.Select(i => i.ImageUrl).FirstOrDefault())).ToListAsync();
        return ApiOutcome.Ok(new RoomSearchResponse(rooms, total, page, pageSize));
    }
    public async Task<ApiOutcome> Detail(Guid id)
    {
        var room = await db.Rooms.AsNoTracking().AsSplitQuery().Where(x => x.RoomId == id && x.Status == "Available")
            .Select(x => new { x.RoomId, x.UserId, x.Title, x.Type, x.Status, x.Address, x.Area,
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
            room.averageRating, room.ratingCount));
    }
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await db.Rooms.AsNoTracking()
        .Where(x => x.UserId == userId).OrderBy(x => x.Title).ThenBy(x => x.RoomId)
        .Skip(skip).Take(pageSize)
        .Select(x => new RoomMineResponse(x.RoomId, x.UserId, x.Status, x.Type,
            x.Title, x.Address, x.Area, x.ElectricityFee, x.WaterFee, x.ServiceFee,
            x.Deposit, x.Price, x.Description, x.Rule)).ToListAsync());
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

        Apply(room, request);
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
    }

    private static bool ValidRoom(RoomRequest x) =>
        !string.IsNullOrWhiteSpace(x.Title) && x.Title.Trim().Length <= 255 &&
        (x.Type is null || x.Type.Length <= 50) &&
        (x.Status is null or "Available" or "Rented" or "Draft" or "Hidden" or "Deleted") &&
        ValidAmount(x.Area, 99999999.99m) &&
        ValidAmount(x.Price) && ValidAmount(x.ElectricityFee) &&
        ValidAmount(x.WaterFee) && ValidAmount(x.ServiceFee) &&
        ValidAmount(x.Deposit);

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
