using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class AppointmentService(IAppointmentRepository appointments, IRoomRepository rooms) : IAppointmentService
{
    public async Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        return ApiOutcomeResponse.Ok(await appointments.ListForUserAsync(userId, skip, pageSize,
        x => new AppointmentResponse
        {
            AppointmentId = x.AppointmentId,
            UserId = x.UserId,
            RoomId = x.RoomId,
            AppointmentDate = x.AppointmentDate,
            AppointmentTime = x.AppointmentTime,
            Status = x.Status,
            Description = x.Description,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            RoomTitle = x.Room.Title,
            GuestName = x.User.Name
        }));
    }
    public async Task<ApiOutcomeResponse> Create(Guid userId, AppointmentRequest request)
    {
        var room = await rooms.FindAsync(request.RoomId);
        if (room is null || !await rooms.IsBookableAsync(request.RoomId)) return ApiOutcomeResponse.NotFound();
        if (room.UserId == userId) return ApiOutcomeResponse.BadRequest("Cannot book your own room.");
        if (request.AppointmentDate.ToDateTime(request.AppointmentTime) <= DateTime.Now)
            return ApiOutcomeResponse.BadRequest("Appointment must be in the future.");
        var item = new Appointment { AppointmentId = Guid.NewGuid(), UserId = userId,
            RoomId = room.RoomId, AppointmentDate = request.AppointmentDate,
            AppointmentTime = request.AppointmentTime, Description = request.Description,
            Status = "Pending" };
        appointments.Add(item);
        await appointments.SaveChangesAsync();
        return ApiOutcomeResponse.Created();
    }
    public async Task<ApiOutcomeResponse> UpdateStatus(Guid userId, Guid id, StatusRequest request)
    {
        var item = await appointments.GetWithRoomAsync(id);
        if (item is null) return ApiOutcomeResponse.NotFound();
        var owner = item.Room.UserId == userId;
        var guest = item.UserId == userId;
        var valid = item.Status == "Pending" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status is "Confirmed" or "Rejected")) ||
            item.Status == "Confirmed" && ((guest && request.Status == "Cancelled") ||
            (owner && request.Status == "Completed"));
        if (!valid) return ApiOutcomeResponse.Conflict("Invalid status transition or permission.");
        var changed = await appointments.ChangeStatusAsync(id, item.Status, request.Status, DateTime.Now);
        if (changed == 0) return ApiOutcomeResponse.Conflict("Appointment status changed. Reload it and try again.");
        return ApiOutcomeResponse.Ok();
    }
}
