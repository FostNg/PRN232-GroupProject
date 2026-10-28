namespace RoomRum.BLL.Services.Implementations;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

public sealed class FavoriteService(IFavoriteRepository favorites, IRoomRepository rooms) : IFavoriteService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await favorites.ListForUserAsync(userId, skip, pageSize,
            x => new FavoriteSummaryResponse(x.FavoriteId, x.RoomId, x.Room.Title,
                x.Room.Price, x.Room.RoomImages.Select(i => i.ImageUrl).FirstOrDefault())));
    }
    public async Task<ApiOutcome> Add(Guid userId, Guid roomId)
    {
        var room = await rooms.FindAsync(roomId);
        if (room is null || !await rooms.IsBookableAsync(roomId)) return ApiOutcome.NotFound();
        var favorite = new Favorite { FavoriteId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
        var created = await favorites.AddIfMissingAsync(favorite);
        var response = new FavoriteMutationResponse(roomId);
        return created ? ApiOutcome.Created(response) : ApiOutcome.Ok(response);
    }
    public async Task<ApiOutcome> Remove(Guid userId, Guid roomId)
    {
        await favorites.RemoveAsync(userId, roomId);
        return ApiOutcome.NoContent();
    }
}
