using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RepairService(IRepairRepository repairs, IRoomTenantRepository tenants) : IRepairService
{
    public async Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await repairs.ListForUserAsync(userId, skip, pageSize,
        x => new RepairResponse
        {
            MaintenanceId = x.MaintenanceId,
            UserId = x.UserId,
            RoomId = x.RoomId,
            Priority = x.Priority,
            RepairItems = x.RepairItems,
            Description = x.Description,
            Status = x.Status,
            ExpectingDate = x.ExpectingDate,
            Response = x.Response,
            RoomTitle = x.Room.Title,
            TenantName = x.User.Name
        }));
    }
    public async Task<ApiOutcomeResponse> Create(Guid userId, RepairRequest request)
    {
        if (request.Priority?.Length > 50) return ApiOutcomeResponse.BadRequest("Priority exceeds the database column limit.");
        var allowed = await tenants.HasStatusAsync(request.RoomId, userId, "Active");
        if (!allowed) return ApiOutcomeResponse.Forbidden();
        var item = new Repairing { MaintenanceId = Guid.NewGuid(), UserId = userId,
            RoomId = request.RoomId, Priority = request.Priority, RepairItems = request.RepairItems,
            Description = request.Description, ExpectingDate = request.ExpectingDate, Status = "Pending" };
        repairs.Add(item); await repairs.SaveChangesAsync();
        return ApiOutcomeResponse.Created();
    }
    public async Task<ApiOutcomeResponse> Respond(Guid userId, Guid id, ConfirmRepairRequest request)
    {
        var item = await repairs.GetWithRoomAsync(id);
        if (item is null) return ApiOutcomeResponse.NotFound();
        if (item.Room.UserId != userId) return ApiOutcomeResponse.Forbidden();
        if (request.Status is not ("Pending" or "InProgress" or "Completed" or "Rejected"))
            return ApiOutcomeResponse.BadRequest("Invalid status.");
        item.Status = request.Status; item.Response = request.Response;
        await repairs.SaveChangesAsync(); return ApiOutcomeResponse.Ok();
    }
}
