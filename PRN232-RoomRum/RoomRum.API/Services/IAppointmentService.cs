using RoomRum.API.Common;
using RoomRum.API.DTOs.Appointments;

namespace RoomRum.API.Services;

/// <summary>
/// Viewing appointments. Every method takes the caller's identity and role so
/// authorization is decided in one place rather than in the controller.
/// </summary>
public interface IAppointmentService
{
    Task<CreateAppointmentResponse> CreateAsync(
        int tenantId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AppointmentResponse>> GetMineAsync(
        int userId,
        string role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AppointmentResponse>> GetForListingAsync(
        int landlordId,
        int listingId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse?> GetByIdAsync(
        int userId,
        string role,
        int appointmentId,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse?> UpdateStatusAsync(
        int userId,
        string role,
        int appointmentId,
        string targetStatus,
        CancellationToken cancellationToken = default);
}
