using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Appointments;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

/// <summary>
/// Viewing appointments.
///
/// A Tenant books a slot on an Active listing; the Landlord confirms or rejects
/// it; after the visit the Landlord marks it Completed. The deposit snapshot is
/// captured when the booking is created and drives the payment module.
/// </summary>
[Route("api/[controller]")]
[Authorize]
public sealed class AppointmentsController : ApiControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    // ============================================================
    // TENANT BOOKS A VIEWING
    // POST: /api/Appointments
    // ============================================================

    [HttpPost]
    [Authorize(Roles = UserRoles.Tenant)]
    [ProducesResponseType(
        typeof(ApiResponse<CreateAppointmentResponse>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        var result = await _appointmentService.CreateAsync(
            tenantId,
            request,
            cancellationToken);

        return CreatedEnvelope(
            result,
            "Viewing appointment booked. A chat with the landlord was opened.");
    }

    // ============================================================
    // MY APPOINTMENTS (TENANT OR LANDLORD)
    // GET: /api/Appointments/my
    // ============================================================

    [HttpGet("my")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<AppointmentResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        var result = await _appointmentService.GetMineAsync(
            userId,
            GetRole(),
            status,
            page,
            pageSize,
            cancellationToken);

        return OkEnvelope(result);
    }

    // ============================================================
    // APPOINTMENTS FOR ONE OF THE LANDLORD'S LISTINGS
    // GET: /api/Appointments/listing/{listingId}
    // ============================================================

    [HttpGet("listing/{listingId:int}")]
    [Authorize(Roles = UserRoles.Landlord)]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<AppointmentResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForListing(
        int listingId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var landlordId))
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid listing ID."));
        }

        var result = await _appointmentService.GetForListingAsync(
            landlordId,
            listingId,
            status,
            page,
            pageSize,
            cancellationToken);

        return OkEnvelope(result);
    }

    // ============================================================
    // APPOINTMENT DETAIL
    // GET: /api/Appointments/{appointmentId}
    // ============================================================

    [HttpGet("{appointmentId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<AppointmentResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int appointmentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        if (appointmentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid appointment ID."));
        }

        var result = await _appointmentService.GetByIdAsync(
            userId,
            GetRole(),
            appointmentId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Appointment not found."));
        }

        return OkEnvelope(result);
    }

    // ============================================================
    // UPDATE STATUS
    // PATCH: /api/Appointments/{appointmentId}/status
    // ============================================================

    [HttpPatch("{appointmentId:int}/status")]
    [ProducesResponseType(
        typeof(ApiResponse<AppointmentResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        int appointmentId,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingIdentity();
        }

        if (appointmentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid appointment ID."));
        }

        var result = await _appointmentService.UpdateStatusAsync(
            userId,
            GetRole(),
            appointmentId,
            request.Status,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Appointment not found."));
        }

        return OkEnvelope(
            result,
            $"Appointment moved to {result.Status}.");
    }
}
