namespace RoomRum.BLL.Services.Implementations;

using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

public sealed class RatingService(IRatingRepository ratings, IRoomRepository rooms,
    IRoomTenantRepository tenants) : IRatingService
{
    public async Task<ApiOutcome> List(Guid roomId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        if (!await rooms.IsPublicAsync(roomId))
            return ApiOutcome.NotFound();
        return ApiOutcome.Ok(await ratings.ListAsync(roomId, skip, pageSize,
            x => new RatingSummaryResponse(x.RatingId, x.Rating, x.Comment,
                x.RatingDate, x.User.Name)));
    }
    public async Task<ApiOutcome> Upsert(Guid userId, Guid roomId, RatingRequest request)
    {
        if (request.Rating is < 1 or > 5) return ApiOutcome.BadRequest("Rating must be 1-5.");
        if (!await tenants.HasStatusAsync(roomId, userId, "Active", "Ended"))
            return ApiOutcome.Forbidden();
        var rating = await ratings.GetForTenantAsync(roomId, userId);
        if (rating is null)
        {
            rating = new RoomRating { RatingId = Guid.NewGuid(), RoomId = roomId, UserId = userId };
            ratings.Add(rating);
        }
        rating.Rating = request.Rating; rating.Comment = request.Comment; rating.RatingDate = DateTime.Now;
        await ratings.SaveChangesAsync(); return ApiOutcome.Ok(new RatingMutationResponse(rating.RatingId));
    }
}
