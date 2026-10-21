using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.Common;
using RoomRum.API.DTOs.Payments;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

/// <summary>
/// Booking-deposit payments.
///
/// Flow: a Tenant books a viewing, the Landlord confirms it, the Tenant creates
/// the deposit order and then completes it. Completing a payment is the only
/// way a listing reaches Reserved, and the database applies that transition
/// atomically together with rejecting competing pending appointments.
/// </summary>
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Tenant)]
public sealed class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // ============================================================
    // CREATE DEPOSIT ORDER
    // POST: /api/Payments
    // ============================================================

    [HttpPost]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentResponse>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        var result = await _paymentService.CreateAsync(
            tenantId,
            request,
            cancellationToken);

        return CreatedEnvelope(
            result,
            "Deposit order created. Complete the payment to reserve the room.");
    }

    // ============================================================
    // MY PAYMENTS
    // GET: /api/Payments/my
    // ============================================================

    [HttpGet("my")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<PaymentResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        var result = await _paymentService.GetMineAsync(
            tenantId,
            status,
            page,
            pageSize,
            cancellationToken);

        return OkEnvelope(result);
    }

    // ============================================================
    // PAYMENT DETAIL
    // GET: /api/Payments/{paymentId}
    // ============================================================

    [HttpGet("{paymentId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int paymentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (paymentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid payment ID."));
        }

        var result = await _paymentService.GetByIdAsync(
            tenantId,
            paymentId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Payment not found."));
        }

        return OkEnvelope(result);
    }

    // ============================================================
    // COMPLETE OR FAIL THE PAYMENT (SIMULATED GATEWAY CALLBACK)
    // POST: /api/Payments/{paymentId}/confirm
    // ============================================================

    [HttpPost("{paymentId:int}/confirm")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentResultResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        int paymentId,
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (paymentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid payment ID."));
        }

        var result = await _paymentService.ConfirmAsync(
            tenantId,
            paymentId,
            request,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Payment not found."));
        }

        return OkEnvelope(result, result.Message);
    }

    // ============================================================
    // RETRY A FAILED ORDER
    // POST: /api/Payments/{paymentId}/retry
    // ============================================================

    [HttpPost("{paymentId:int}/retry")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(
        int paymentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (paymentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid payment ID."));
        }

        var result = await _paymentService.RetryAsync(
            tenantId,
            paymentId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Payment not found."));
        }

        return OkEnvelope(
            result,
            "Payment reset to Pending. You can attempt the deposit again.");
    }

    // ============================================================
    // CANCEL AN UNPAID ORDER
    // DELETE: /api/Payments/{paymentId}
    // ============================================================

    [HttpDelete("{paymentId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<PaymentResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        int paymentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var tenantId))
        {
            return MissingIdentity();
        }

        if (paymentId <= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid payment ID."));
        }

        var result = await _paymentService.CancelAsync(
            tenantId,
            paymentId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(ApiResponse.Fail("Payment not found."));
        }

        return OkEnvelope(result, "Payment order cancelled.");
    }
}
