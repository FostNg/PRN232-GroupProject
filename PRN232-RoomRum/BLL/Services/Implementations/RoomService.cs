using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RoomService(IRoomRepository rooms, IAmenityRepository amenities) : IRoomService
{
    public async Task<ApiOutcome> Search(RoomSearchRequest request)
    {
        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 100 ||
            (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            return ApiOutcome.BadRequest("Invalid pagination.");
        if (!ValidAmount(request.MinPrice) || !ValidAmount(request.MaxPrice) ||
            !ValidAmount(request.MinArea, 99999999.99m) ||
            !ValidAmount(request.MaxArea, 99999999.99m) ||
            !ValidAmount(request.MaxDeposit) || !ValidAmount(request.MaxMoveInCost) ||
            !ValidAmount(request.MaxMonthlyCost) || request.MinPrice > request.MaxPrice ||
            request.MinArea > request.MaxArea || request.Occupants is < 1 or > 10 ||
            request.MonthlyKwh is < 0 or > 2000 ||
            request.Q?.Length > 200 || request.Type?.Length > 50)
            return ApiOutcome.BadRequest("Invalid search range or estimated cost inputs.");
        var hasOrigin = request.Latitude.HasValue && request.Longitude.HasValue;
        if (!ValidCoordinates(request.Latitude, request.Longitude) ||
            request.MaxDistanceKm.HasValue && (!hasOrigin || !double.IsFinite(request.MaxDistanceKm.Value) ||
                request.MaxDistanceKm is <= 0 or > 100) ||
            request.Sort is not (null or "relevance" or "priceAsc" or "priceDesc" or
                "areaDesc" or "monthlyCostAsc" or "distance") ||
            request.Sort == "distance" && !hasOrigin)
            return ApiOutcome.BadRequest("Invalid location, distance or sort option.");

        var amenityIds = (request.AmenityIds ?? []).Concat(
            request.AmenityId is Guid oldId ? [oldId] : []).Distinct().ToArray();
        var criteria = new RoomSearchCriteria(request.Q, request.Type, request.MinPrice,
            request.MaxPrice, request.MinArea, request.MaxArea, request.MaxDeposit,
            request.MaxMoveInCost, request.MaxMonthlyCost, request.Occupants,
            request.MonthlyKwh, amenityIds, request.Latitude, request.Longitude,
            request.MaxDistanceKm, request.Sort, (request.Page - 1) * request.PageSize,
            request.PageSize);
        var result = await rooms.SearchAdvancedAsync(criteria);
        var items = result.Items.Select(x => new RoomSummaryResponse(x.RoomId, x.UserId,
            x.Title, x.Address, x.Type, x.Price, x.Area, x.Status, x.ImageUrl,
            MonthlyCost(x, request.Occupants, request.MonthlyKwh), x.DistanceKm,
            x.Latitude, x.Longitude)).ToList();
        return ApiOutcome.Ok(new RoomSearchResponse(items, result.Total, request.Page, request.PageSize));
    }

    private static decimal? MonthlyCost(RoomSearchItem room, int occupants, int monthlyKwh) =>
        room.Price is decimal price && room.ServiceFee is decimal service &&
        room.WaterFee is decimal water && (monthlyKwh == 0 || room.ElectricityFee.HasValue)
            ? price + service + water * occupants + (room.ElectricityFee ?? 0) * monthlyKwh
            : null;
    public async Task<ApiOutcome> Detail(Guid id, Guid? viewerId)
    {
        var room = await rooms.GetPublicDetailAsync(id, viewerId,
            x => new { x.RoomId, x.UserId, x.Title, x.Type, x.Status, x.Address, x.Area,
                x.Latitude, x.Longitude,
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
            room.averageRating, room.ratingCount, room.Latitude, room.Longitude));
    }
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await rooms.ListOwnedAsync(userId, skip, pageSize,
        x => new RoomMineResponse(x.RoomId, x.UserId, x.Status, x.Type,
            x.Title, x.Address, x.Area, x.ElectricityFee, x.WaterFee, x.ServiceFee,
            x.Deposit, x.Price, x.Description, x.Rule,
            x.Amenities.OrderBy(a => a.AmenityName)
                .Select(a => new RoomAmenityResponse(a.AmenityId, a.AmenityName)).ToList(),
            x.RoomImages.OrderBy(i => i.RoomImageId)
                .Select(i => new RoomImageResponse(i.RoomImageId, i.ImageUrl)).ToList(),
            x.Latitude, x.Longitude)));
    }
    public async Task<ApiOutcome> Create(Guid userId, RoomRequest request)
    {
        if (!ValidRoom(request) || !ValidCoordinates(request.Latitude, request.Longitude) ||
            request.ClearCoordinates && request.Latitude.HasValue)
            return ApiOutcome.BadRequest("Invalid room values, coordinates or database column limit exceeded.");
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
        if (!ValidUpdate(request) || !ValidCoordinates(request.Latitude, request.Longitude) ||
            request.ClearCoordinates && request.Latitude.HasValue)
            return ApiOutcome.BadRequest("Invalid room values, coordinates or database column limit exceeded.");

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

        if (request.ReplaceImageUrls)
        {
            var imageUrls = NormalizeImageUrls(request.ImageUrls, id);
            if (imageUrls is null) return ApiOutcome.BadRequest("Unknown amenity or invalid image URL.");
            var reusable = room.RoomImages.GroupBy(x => x.ImageUrl)
                .ToDictionary(x => x.Key, x => new Queue<RoomImage>(x));
            var preserved = new HashSet<Guid>();
            foreach (var url in imageUrls)
            {
                if (reusable.TryGetValue(url, out var matches) && matches.Count > 0)
                    preserved.Add(matches.Dequeue().RoomImageId);
                else
                    rooms.AddImage(new RoomImage { RoomImageId = Guid.NewGuid(), RoomId = id, ImageUrl = url });
            }
            foreach (var image in room.RoomImages.Where(x => !preserved.Contains(x.RoomImageId)).ToList())
                rooms.RemoveImage(image);
        }
        else if (request.ImageUrls is { Count: > 0 })
        {
            var imageUrls = NormalizeImageUrls(request.ImageUrls, id);
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

        var addressChanged = !string.IsNullOrWhiteSpace(request.Address) &&
            !string.Equals(room.Address?.Trim(), request.Address.Trim(), StringComparison.OrdinalIgnoreCase);
        ApplyUpdate(room, request);
        if (request.ClearCoordinates)
        {
            room.Latitude = null;
            room.Longitude = null;
        }
        else if (request.Latitude.HasValue)
        {
            room.Latitude = request.Latitude;
            room.Longitude = request.Longitude;
        }
        else if (addressChanged)
        {
            room.Latitude = null;
            room.Longitude = null;
        }
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
        room.Latitude = x.Latitude; room.Longitude = x.Longitude;
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

    private static bool ValidCoordinates(double? latitude, double? longitude) =>
        latitude.HasValue == longitude.HasValue &&
        (latitude is null || double.IsFinite(latitude.Value) && latitude is >= -90 and <= 90 &&
            double.IsFinite(longitude!.Value) && longitude is >= -180 and <= 180);

    private async Task<bool> AddRelations(Room room, RoomRequest request)
    {
        var ids = request.AmenityIds?.Distinct().ToList() ?? [];
        var selectedAmenities = await amenities.GetByIdsAsync(ids);
        if (selectedAmenities.Count != ids.Count) return false;
        foreach (var amenity in selectedAmenities) room.Amenities.Add(amenity);
        var imageUrls = NormalizeImageUrls(request.ImageUrls, room.RoomId);
        if (imageUrls is null) return false;
        foreach (var url in imageUrls)
        {
            room.RoomImages.Add(new RoomImage { RoomImageId = Guid.NewGuid(), ImageUrl = url });
        }
        return true;
    }

    private static List<string>? NormalizeImageUrls(List<string>? input, Guid roomId)
    {
        var result = new List<string>();
        foreach (var raw in input ?? [])
        {
            var path = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (raw.StartsWith('/') && path.Length == 6 &&
                path[0] == "api" && path[1] == "rooms" &&
                Guid.TryParse(path[2], out var urlRoomId) && urlRoomId == roomId &&
                path[3] == "images" && Guid.TryParse(path[4], out _) &&
                path[5] == "content")
            {
                result.Add(raw);
                continue;
            }
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https")) return null;
            var normalized = new UriBuilder(uri) { Host = uri.IdnHost }.Uri.AbsoluteUri;
            if (normalized.Any(c => c > 127)) return null;
            result.Add(normalized);
        }
        return result;
    }
}
