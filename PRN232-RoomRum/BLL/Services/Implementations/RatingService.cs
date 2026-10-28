using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RatingService(IRatingRepository ratings, IRoomRepository rooms,
    IRoomTenantRepository tenants) : IRatingService
{
    public async Task<ApiOutcomeResponse> List(Guid roomId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        if (!await rooms.IsPublicAsync(roomId))
            return ApiOutcomeResponse.NotFound();
        return ApiOutcomeResponse.Ok(await ratings.ListAsync(roomId, skip, pageSize,
            x => new RatingResponse
            {
                RatingId = x.RatingId,
                Rating = x.Rating,
                Comment = x.Comment,
                RatingDate = x.RatingDate,
                User = x.User.Name
            }));
    }
    public async Task<ApiOutcomeResponse> Upsert(Guid userId, Guid roomId, RatingRequest request)
    {
        if (request.Rating is < 1 or > 5) return ApiOutcomeResponse.BadRequest("Rating must be 1-5.");
        if (!await tenants.HasStatusAsync(roomId, userId, "Active", "Ended"))
            return ApiOutcomeResponse.Forbidden();
        var rating = await ratings.GetForTenantAsync(roomId, userId);
        if (rating is null)
        {
            rating = new RoomRating { RatingId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
            ratings.Add(rating);
        }
        rating.Rating = request.Rating; rating.Comment = request.Comment; rating.RatingDate = DateTime.Now;
        await ratings.SaveChangesAsync(); return ApiOutcomeResponse.Ok();
    }
}
