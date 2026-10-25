using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class AppointmentService(IAppointmentRepository appointments, IRoomRepository rooms) : IAppointmentService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        return ApiOutcome.Ok(await appointments.ListForUserAsync(userId, skip, pageSize,
        x => new AppointmentSummaryResponse(x.AppointmentId, x.UserId, x.RoomId,
            x.AppointmentDate, x.AppointmentTime, x.Status, x.Description,
            x.CreatedAt, x.UpdatedAt, x.Room.Title)));
    }
    public async Task<ApiOutcome> Create(Guid userId, AppointmentRequest request)
    {
        var room = await rooms.FindAsync(request.RoomId);
        if (room is null || !await rooms.IsBookableAsync(request.RoomId)) return ApiOutcome.NotFound();
        if (room.UserId == userId) return ApiOutcome.BadRequest("Cannot book your own room.");
        if (request.AppointmentDate.ToDateTime(request.AppointmentTime) <= DateTime.Now)
            return ApiOutcome.BadRequest("Appointment must be in the future.");
        var item = new Appointment { AppointmentId = Guid.NewGuid(), UserId = userId,
            RoomId = room.RoomId, AppointmentDate = request.AppointmentDate,
            AppointmentTime = request.AppointmentTime, Description = request.Description,
            Status = "Pending" };
        appointments.Add(item);
        await appointments.SaveChangesAsync();
        return ApiOutcome.Created(new AppointmentMutationResponse(item.AppointmentId, item.Status));
    }
    public async Task<ApiOutcome> UpdateStatus(Guid userId, Guid id, StatusRequest request)
    {
        var item = await appointments.GetWithRoomAsync(id);
        if (item is null) return ApiOutcome.NotFound();
        var owner = item.Room.UserId == userId;
        var guest = item.UserId == userId;
        var valid = item.Status == "Pending" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status is "Confirmed" or "Rejected")) ||
            item.Status == "Confirmed" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status == "Completed"));
        if (!valid) return ApiOutcome.Conflict("Invalid status transition or permission.");
        var changed = await appointments.ChangeStatusAsync(id, item.Status, request.Status, DateTime.Now);
        if (changed == 0) return ApiOutcome.Conflict("Appointment status changed. Reload it and try again.");
        return ApiOutcome.Ok(new AppointmentMutationResponse(item.AppointmentId, request.Status));
    }
}
