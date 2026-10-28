using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class SharingService(ISharingRepository sharings, IRoomRepository rooms,
    IRoomTenantRepository tenants) : ISharingService
{
    public async Task<ApiOutcomeResponse> List(Guid? roomId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await sharings.ListAsync(roomId, skip, pageSize,
        x => new SharingResponse
        {
            SharingId = x.SharingId,
            UserId = x.UserId,
            RoomId = x.RoomId,
            Title = x.Title,
            Quantity = x.Quantity,
            Gender = x.Gender,
            SharingFee = x.SharingFee,
            Introduction = x.Introduction,
            Rule = x.Rule,
            RoomTitle = x.Room.Title,
            Author = x.User.Name
        }));
    }
    public async Task<ApiOutcomeResponse> Create(Guid userId, SharingRequest request)
    {
        var allowed = await rooms.IsOwnerAsync(request.RoomId, userId)
            || await tenants.HasStatusAsync(request.RoomId, userId, "Active");
        if (!allowed) return ApiOutcomeResponse.Forbidden();
        if (request.Quantity is < 1 || request.SharingFee is < 0 or > 9999999999999999.99m ||
            request.SharingFee is decimal fee && decimal.Round(fee, 2) != fee ||
            request.Title?.Length > 255 || request.Gender?.Length > 20)
            return ApiOutcomeResponse.BadRequest("Invalid quantity, fee or database column limit exceeded.");
        var item = new Sharing { SharingId = Guid.NewGuid(), UserId = userId, RoomId = request.RoomId,
            Title = request.Title, Quantity = request.Quantity, Gender = request.Gender,
            SharingFee = request.SharingFee, Introduction = request.Introduction, Rule = request.Rule };
        sharings.Add(item); await sharings.SaveChangesAsync();
        return ApiOutcomeResponse.Created();
    }
    public async Task<ApiOutcomeResponse> Delete(Guid userId, bool isAdmin, Guid id)
    {
        var item = await sharings.FindAsync(id);
        if (item is null) return ApiOutcomeResponse.NotFound();
        if (item.UserId != userId && !isAdmin) return ApiOutcomeResponse.Forbidden();
        sharings.Remove(item); await sharings.SaveChangesAsync(); return ApiOutcomeResponse.NoContent();
    }
}
