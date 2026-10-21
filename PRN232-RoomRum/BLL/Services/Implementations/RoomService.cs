using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RoomService(IRoomRepository rooms, IAmenityRepository amenities) : IRoomService
{
    public async Task<ApiOutcome> Search(string? q, string? type,
        decimal? minPrice, decimal? maxPrice,
        Guid? amenityId, int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        if (!ValidAmount(minPrice) || !ValidAmount(maxPrice) || minPrice > maxPrice)
            return ApiOutcome.BadRequest("Invalid price range or decimal precision.");
        var result = await rooms.SearchAsync(q, type, minPrice, maxPrice, amenityId,
            (page - 1) * pageSize, pageSize,
            x => new RoomSummaryResponse(x.RoomId, x.UserId, x.Title, x.Address,
                x.Type, x.Price, x.Area, x.Status,
                x.RoomImages.Select(i => i.ImageUrl).FirstOrDefault()));
        return ApiOutcome.Ok(new RoomSearchResponse(result.Items, result.Total, page, pageSize));
    }
    public async Task<ApiOutcome> Detail(Guid id, Guid? viewerId)
    {
        var room = await rooms.GetPublicDetailAsync(id, viewerId,
            x => new { x.RoomId, x.UserId, x.Title, x.Type, x.Status, x.Address, x.Area,
                x.ElectricityFee, x.WaterFee, x.ServiceFee, x.Deposit, x.Price, x.Description, x.Rule,
                owner = new { x.User.UserId, x.User.Name, x.User.Phone },
                amenities = x.Amenities.Select(a => new { a.AmenityId, a.AmenityName }),
                images = x.RoomImages.Select(i => new { i.RoomImageId, i.ImageUrl }),
                averageRating = x.RoomRatings.Select(r => (double?)r.Rating).Average(),
                ratingCount = x.RoomRatings.Count });
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
        return ApiOutcome.Ok(await rooms.ListOwnedAsync(userId, skip, pageSize,
        x => new RoomMineResponse(x.RoomId, x.UserId, x.Status, x.Type,
            x.Title, x.Address, x.Area, x.ElectricityFee, x.WaterFee, x.ServiceFee,
            x.Deposit, x.Price, x.Description, x.Rule)));
    }
    public async Task<ApiOutcome> Create(Guid userId, RoomRequest request)
    {
        if (!ValidRoom(request)) return ApiOutcome.BadRequest("Invalid room values or database column limit exceeded.");
        var room = new Room { RoomId = Guid.NewGuid(), UserId = userId, Status = "Pending" };
        Apply(room, request);
        if (!await AddRelations(room, request)) return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");
        rooms.Add(room);
        await rooms.SaveChangesAsync();
        return ApiOutcome.Created(new RoomMutationResponse(room.RoomId));
    }
    public async Task<ApiOutcome> Update(Guid userId, bool isAdmin, Guid id, RoomRequest request)
    {
        var room = await rooms.GetWithRelationsAsync(id);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId && !isAdmin) return ApiOutcome.Forbidden();
        if (!ValidUpdate(request)) return ApiOutcome.BadRequest("Invalid room values or database column limit exceeded.");

        if (request.AmenityIds is { Count: > 0 })
        {
            var amenityIds = request.AmenityIds.Distinct().ToList();
            var selectedAmenities = await amenities.GetByIdsAsync(amenityIds);
            if (selectedAmenities.Count != amenityIds.Count)
                return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");
            foreach (var amenity in room.Amenities.Where(x => !amenityIds.Contains(x.AmenityId)).ToList())
                room.Amenities.Remove(amenity);
            foreach (var amenity in selectedAmenities.Where(x => room.Amenities.All(a => a.AmenityId != x.AmenityId)))
                room.Amenities.Add(amenity);
        }

        if (request.ImageUrls is { Count: > 0 })
        {
            var imageUrls = NormalizeImageUrls(request.ImageUrls);
            if (imageUrls is null) return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");
            var oldImages = room.RoomImages.ToList();
            foreach (var url in imageUrls.Distinct(StringComparer.Ordinal))
            {
                var matches = oldImages.Where(x => string.Equals(x.ImageUrl, url, StringComparison.Ordinal)).ToList();
                if (matches.Count > 0)
                    foreach (var image in matches) rooms.RemoveImage(image);
                else
                    rooms.AddImage(new RoomImage { RoomImageId = Guid.NewGuid(), RoomId = id, ImageUrl = url });
            }
        }

        ApplyUpdate(room, request);
        await rooms.SaveChangesAsync();
        return ApiOutcome.Ok(new RoomMutationResponse(room.RoomId));
    }
    public async Task<ApiOutcome> Hide(Guid userId, bool isAdmin, Guid id)
    {
        var room = await rooms.FindAsync(id);
        if (room is null) return ApiOutcome.NotFound();
        if (room.UserId != userId && !isAdmin) return ApiOutcome.Forbidden();
        room.Status = "Hidden";
        await rooms.SaveChangesAsync();
        return ApiOutcome.NoContent();
    }
    public async Task<ApiOutcome> Amenities(int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await amenities.ListAsync(skip, pageSize,
            x => new RoomAmenityResponse(x.AmenityId, x.AmenityName)));
    }

    private static void Apply(Room room, RoomRequest x)
    {
        room.Type = x.Type; room.Title = x.Title?.Trim(); room.Address = x.Address;
        room.Area = x.Area; room.ElectricityFee = x.ElectricityFee; room.WaterFee = x.WaterFee;
        room.ServiceFee = x.ServiceFee; room.Deposit = x.Deposit; room.Price = x.Price;
        room.Description = x.Description; room.Rule = x.Rule;
    }

    private static void ApplyUpdate(Room room, RoomRequest x)
    {
        if (!string.IsNullOrWhiteSpace(x.Type)) room.Type = x.Type;
        if (!string.IsNullOrWhiteSpace(x.Title)) room.Title = x.Title.Trim();
        if (!string.IsNullOrWhiteSpace(x.Address)) room.Address = x.Address;
        if (x.Area.HasValue) room.Area = x.Area;
        if (x.ElectricityFee.HasValue) room.ElectricityFee = x.ElectricityFee;
        if (x.WaterFee.HasValue) room.WaterFee = x.WaterFee;
        if (x.ServiceFee.HasValue) room.ServiceFee = x.ServiceFee;
        if (x.Deposit.HasValue) room.Deposit = x.Deposit;
        if (x.Price.HasValue) room.Price = x.Price;
        if (!string.IsNullOrWhiteSpace(x.Description)) room.Description = x.Description;
        if (!string.IsNullOrWhiteSpace(x.Rule)) room.Rule = x.Rule;
    }

    private static bool ValidUpdate(RoomRequest x) =>
        (string.IsNullOrWhiteSpace(x.Title) || x.Title.Trim().Length <= 255) &&
        (string.IsNullOrWhiteSpace(x.Type) || x.Type.Length <= 50) &&
        ValidAmount(x.Area, 99999999.99m) &&
        ValidAmount(x.Price) && ValidAmount(x.ElectricityFee) &&
        ValidAmount(x.WaterFee) && ValidAmount(x.ServiceFee) &&
        ValidAmount(x.Deposit);

    private static bool ValidRoom(RoomRequest x) =>
        !string.IsNullOrWhiteSpace(x.Title) && x.Title.Trim().Length <= 255 &&
        (x.Type is null || x.Type.Length <= 50) &&
        ValidAmount(x.Area, 99999999.99m) &&
        ValidAmount(x.Price) && ValidAmount(x.ElectricityFee) &&
        ValidAmount(x.WaterFee) && ValidAmount(x.ServiceFee) &&
        ValidAmount(x.Deposit);

    private static bool ValidAmount(decimal? value, decimal max = 9999999999999999.99m) =>
        value is null || value >= 0 && value <= max && decimal.Round(value.Value, 2) == value.Value;

    private async Task<bool> AddRelations(Room room, RoomRequest request)
    {
        var ids = request.AmenityIds?.Distinct().ToList() ?? [];
        var selectedAmenities = await amenities.GetByIdsAsync(ids);
        if (selectedAmenities.Count != ids.Count) return false;
        foreach (var amenity in selectedAmenities) room.Amenities.Add(amenity);
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
