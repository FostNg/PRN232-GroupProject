using RoomRum.API.Common;
using RoomRum.API.DTOs.Payments;

namespace RoomRum.API.Services;

/// <summary>
/// Booking-deposit payments for confirmed viewing appointments.
/// </summary>
public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(
        int tenantId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PaymentResponse>> GetMineAsync(
        int tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse?> GetByIdAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default);

    Task<PaymentResultResponse?> ConfirmAsync(
        int tenantId,
        int paymentId,
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a Failed payment back to Pending so it can be attempted again.
    /// The database requires this step: dbo.TR_Payments_Integrity only allows
    /// Failed -> Pending -> Paid, never Failed -> Paid directly.
    /// </summary>
    Task<PaymentResponse?> RetryAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse?> CancelAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default);
}
