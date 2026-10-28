using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class FavoriteService(IFavoriteRepository favorites, IRoomRepository rooms) : IFavoriteService
{
    public async Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await favorites.ListForUserAsync(userId, skip, pageSize,
            x => new FavoriteResponse
            {
                FavoriteId = x.FavoriteId,
                RoomId = x.RoomId,
                Title = x.Room.Title,
                Price = x.Room.Price,
                ImageUrl = x.Room.RoomImages.Select(i => i.ImageUrl).FirstOrDefault()
            }));
    }
    public async Task<ApiOutcomeResponse> Add(Guid userId, Guid roomId)
    {
        var room = await rooms.FindAsync(roomId);
        if (room is null || !await rooms.IsBookableAsync(roomId)) return ApiOutcomeResponse.NotFound();
        var favorite = new Favorite { FavoriteId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
        var created = await favorites.AddIfMissingAsync(favorite);
        return created ? ApiOutcomeResponse.Created() : ApiOutcomeResponse.Ok();
    }
    public async Task<ApiOutcomeResponse> Remove(Guid userId, Guid roomId)
    {
        await favorites.RemoveAsync(userId, roomId);
        return ApiOutcomeResponse.NoContent();
    }
}
