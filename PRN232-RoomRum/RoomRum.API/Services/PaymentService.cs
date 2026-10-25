using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.DTOs.Payments;
using RoomRum.API.Models;

namespace RoomRum.API.Services;

/// <summary>
/// Booking-deposit payments.
///
/// The database owns the hard guarantees (dbo.TR_Payments_Integrity):
///   - a new payment starts Pending and its amount must equal the appointment
///     deposit snapshot;
///   - Paid requires a Confirmed appointment on an Active listing;
///   - a listing can only ever have one successful payment;
///   - Paid is terminal and must carry a transaction code and PaidAt;
///   - marking a payment Paid atomically moves the listing to Reserved and
///     rejects the listing's other Pending appointments.
///
/// This service validates the same rules first so the mobile app receives a
/// useful message, then lets the trigger apply the side effects.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly RoomRumDbContext _context;

    public PaymentService(RoomRumDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// True when the database rejected the insert because a payment for this
    /// appointment already exists (UQ_Payments_Appointment, error 2601/2627).
    /// Used to turn a concurrent duplicate into an idempotent response.
    /// </summary>
    private static bool IsDuplicatePaymentForAppointment(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    // ============================================================
    // CREATE ORDER
    // ============================================================

    public async Task<PaymentResponse> CreateAsync(
        int tenantId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            throw new ArgumentException("Payment method is required.");
        }

        var method = request.PaymentMethod.Trim();

        if (!PaymentMethods.All.Contains(method, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Payment method must be one of: " +
                $"{string.Join(", ", PaymentMethods.All)}.");
        }

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Listing)
            .FirstOrDefaultAsync(
                a => a.AppointmentId == request.AppointmentId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Appointment does not exist.");

        if (appointment.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                "You can only pay for your own appointments.");
        }

        var existing = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (existing is not null)
        {
            // Idempotent: return the current order instead of failing, so a
            // retried tap on "Pay deposit" does not create a second row (the
            // database would reject it via UQ_Payments_Appointment).
            return await BuildResponseAsync(
                existing.PaymentId,
                tenantId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "The payment order could not be loaded.");
        }

        if (appointment.Status != AppointmentStatuses.Confirmed)
        {
            throw new InvalidOperationException(
                "A deposit can only be paid for a Confirmed appointment. " +
                $"This appointment is {appointment.Status}.");
        }

        var listing = appointment.Listing;

        if (listing is null || listing.Status != ListingStatuses.Active)
        {
            throw new InvalidOperationException(
                "The listing is no longer available for reservation.");
        }

        var listingAlreadyPaid = await _context.Payments
            .AsNoTracking()
            .AnyAsync(
                p =>
                    p.PaymentStatus == PaymentStatuses.Paid &&
                    p.Appointment!.ListingId == appointment.ListingId,
                cancellationToken);

        if (listingAlreadyPaid)
        {
            throw new InvalidOperationException(
                "This listing already has a successful booking-deposit payment.");
        }

        var payment = new Payment
        {
            AppointmentId = appointment.AppointmentId,

            // Copied from the snapshot; the trigger rejects any other amount.
            Amount = appointment.DepositAmountSnapshot,

            PaymentPurpose = PaymentPurposes.BookingDeposit,
            PaymentMethod = method,
            PaymentStatus = PaymentStatuses.Pending,

            // Explicit whole-second CreatedAt, otherwise a same-second confirm
            // can store PaidAt/UpdatedAt before the rounded CreatedAt and trip
            // CK_Payments_PaidAtRange / CK_Payments_UpdatedAt (error 547).
            CreatedAt = DateTimeExtensions.UtcNowCreatedAt()
        };

        _context.Payments.Add(payment);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicatePaymentForAppointment(exception))
        {
            // A double tap: both requests passed the pre-check above before
            // either row existed, and UQ_Payments_Appointment stopped the
            // second one. Returning the order that won keeps the endpoint
            // idempotent under concurrency instead of surfacing a false
            // "listing was just reserved" conflict to the app.
            _context.Entry(payment).State = EntityState.Detached;

            var winner = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.AppointmentId == appointment.AppointmentId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The payment order could not be created.",
                    exception);

            return await BuildResponseAsync(
                winner.PaymentId,
                tenantId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "The payment order could not be loaded.",
                    exception);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The payment order could not be created. The listing may have " +
                "just been reserved by another tenant.",
                exception);
        }

        return await BuildResponseAsync(
            payment.PaymentId,
            tenantId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The payment order was created but could not be loaded.");
    }

    // ============================================================
    // READ
    // ============================================================

    public async Task<PagedResult<PaymentResponse>> GetMineAsync(
        int tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var query = _context.Payments
            .AsNoTracking()
            .Where(p => p.Appointment!.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();

            if (normalized is not (
                PaymentStatuses.Pending or
                PaymentStatuses.Paid or
                PaymentStatuses.Failed or
                PaymentStatuses.Cancelled))
            {
                throw new ArgumentException(
                    "Status filter must be Pending, Paid, Failed or Cancelled.");
            }

            query = query.Where(p => p.PaymentStatus == normalized);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var paymentIds = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PaymentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.PaymentId)
            .ToListAsync(cancellationToken);

        var rows = paymentIds.Count == 0
            ? []
            : await QueryProjection()
                .Where(row => paymentIds.Contains(row.PaymentId))
                .ToListAsync(cancellationToken);

        var items = paymentIds
            .Select(id => rows.FirstOrDefault(r => r.PaymentId == id))
            .Where(row => row is not null)
            .Select(row => Map(row!))
            .ToList();

        return PagedResult<PaymentResponse>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }

    public async Task<PaymentResponse?> GetByIdAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        return await BuildResponseAsync(
            paymentId,
            tenantId,
            cancellationToken);
    }

    // ============================================================
    // CONFIRM (SIMULATED GATEWAY CALLBACK)
    // ============================================================

    public async Task<PaymentResultResponse?> ConfirmAsync(
        int tenantId,
        int paymentId,
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentStatus))
        {
            throw new ArgumentException("PaymentStatus is required.");
        }

        var targetStatus = request.PaymentStatus.Trim();

        if (targetStatus is not (PaymentStatuses.Paid or PaymentStatuses.Failed))
        {
            throw new ArgumentException(
                "PaymentStatus must be either Paid or Failed. " +
                "Pending and Cancelled are controlled by the server.");
        }

        var payment = await _context.Payments
            .Include(p => p.Appointment!)
                .ThenInclude(a => a.Listing)
            .FirstOrDefaultAsync(
                p => p.PaymentId == paymentId,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var appointment = payment.Appointment
            ?? throw new InvalidOperationException(
                "The appointment for this payment no longer exists.");

        if (appointment.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                "You can only confirm your own payments.");
        }

        var listing = appointment.Listing;

        // ------------------------------------------------------------
        // Status machine guard (mirrors dbo.TR_Payments_Integrity).
        // ------------------------------------------------------------
        // Failed -> Paid is deliberately absent: the database only allows
        // Failed -> Pending -> Paid, so a retry must first clear the failure.
        var allowedFrom = targetStatus == PaymentStatuses.Paid
            ? payment.PaymentStatus == PaymentStatuses.Pending
            : payment.PaymentStatus is PaymentStatuses.Pending
                or PaymentStatuses.Failed;

        if (!allowedFrom)
        {
            throw new InvalidOperationException(
                payment.PaymentStatus == PaymentStatuses.Failed &&
                targetStatus == PaymentStatuses.Paid
                    ? "This payment previously failed. Retry it first " +
                      "(POST /api/Payments/{id}/retry), then confirm it again."
                    : $"A payment in status '{payment.PaymentStatus}' cannot " +
                      $"be moved to '{targetStatus}'.");
        }

        var rejectedAppointments = 0;

        if (targetStatus == PaymentStatuses.Paid)
        {
            // A Paid payment requires a Confirmed appointment on an Active
            // listing (trigger error 52194).
            if (appointment.Status != AppointmentStatuses.Confirmed)
            {
                throw new InvalidOperationException(
                    "The deposit can only be paid while the appointment is " +
                    $"Confirmed. It is currently {appointment.Status}.");
            }

            if (listing is null || listing.Status != ListingStatuses.Active)
            {
                throw new InvalidOperationException(
                    "The listing is no longer available for reservation.");
            }

            var otherPaid = await _context.Payments
                .AsNoTracking()
                .AnyAsync(
                    p =>
                        p.PaymentStatus == PaymentStatuses.Paid &&
                        p.PaymentId != payment.PaymentId &&
                        p.Appointment!.ListingId == appointment.ListingId,
                    cancellationToken);

            if (otherPaid)
            {
                throw new InvalidOperationException(
                    "This listing already has a successful booking-deposit payment.");
            }

            rejectedAppointments = await _context.Appointments
                .AsNoTracking()
                .CountAsync(
                    a =>
                        a.ListingId == appointment.ListingId &&
                        a.AppointmentId != appointment.AppointmentId &&
                        a.Status == AppointmentStatuses.Pending,
                    cancellationToken);

            payment.PaymentStatus = PaymentStatuses.Paid;
            payment.PaidAt = DateTimeExtensions.UtcNowToSecond();
            payment.FailureReason = null;
            payment.TransactionCode = string.IsNullOrWhiteSpace(
                request.TransactionCode)
                ? GenerateTransactionCode()
                : request.TransactionCode.Trim();
        }
        else
        {
            // Failed requires a reason, and PaidAt must stay null
            // (CK_Payments_FailureData / CK_Payments_PaidData).
            var reason = request.FailureReason?.Trim();

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "FailureReason is required when PaymentStatus is Failed.");
            }

            if (reason.Length > 500)
            {
                throw new ArgumentException(
                    "FailureReason must not exceed 500 characters.");
            }

            payment.PaymentStatus = PaymentStatuses.Failed;
            payment.FailureReason = reason;
            payment.PaidAt = null;
        }

        payment.UpdatedAt = DateTimeExtensions.UtcNowToSecond();

        try
        {
            // The trigger reserves the listing and rejects competing Pending
            // appointments inside this same transaction.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The payment could not be completed. The listing may have " +
                "just been reserved by another tenant.",
                exception);
        }

        // Reload the state the trigger changed.
        var refreshed = await BuildResponseAsync(
            payment.PaymentId,
            tenantId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The payment was updated but could not be loaded.");

        var message = targetStatus == PaymentStatuses.Paid
            ? rejectedAppointments > 0
                ? "Payment successful. The listing is now Reserved and " +
                  $"{rejectedAppointments} competing pending appointment(s) " +
                  "were rejected."
                : "Payment successful. The listing is now Reserved."
            : "The payment attempt failed. You can retry the deposit.";

        return new PaymentResultResponse
        {
            Payment = refreshed,
            AppointmentStatus = refreshed.AppointmentStatus,
            ListingStatus = refreshed.ListingStatus,
            RejectedAppointments = rejectedAppointments,
            Message = message
        };
    }

    // ============================================================
    // RETRY A FAILED ORDER
    // ============================================================

    public async Task<PaymentResponse?> RetryAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .Include(p => p.Appointment!)
                .ThenInclude(a => a.Listing)
            .FirstOrDefaultAsync(
                p => p.PaymentId == paymentId,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.Appointment?.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                "You can only retry your own payments.");
        }

        if (!PaymentActionRules.CanRetry(payment.PaymentStatus))
        {
            throw new InvalidOperationException(
                $"Only a Failed payment can be retried. This payment is " +
                $"{payment.PaymentStatus}.");
        }

        var appointment = payment.Appointment;
        var listing = appointment?.Listing;

        // The listing may have been taken while the payment was failing.
        if (appointment is null ||
            appointment.Status != AppointmentStatuses.Confirmed)
        {
            throw new InvalidOperationException(
                "The appointment is no longer Confirmed, so this deposit " +
                "cannot be retried.");
        }

        if (listing is null || listing.Status != ListingStatuses.Active)
        {
            throw new InvalidOperationException(
                "The listing is no longer available for reservation.");
        }

        payment.PaymentStatus = PaymentStatuses.Pending;

        // CK_Payments_FailureData requires FailureReason to be NULL unless the
        // status is exactly Failed.
        payment.FailureReason = null;
        payment.UpdatedAt = DateTimeExtensions.UtcNowToSecond();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The payment could not be retried. The listing may have just " +
                "been reserved by another tenant.",
                exception);
        }

        return await BuildResponseAsync(
            payment.PaymentId,
            tenantId,
            cancellationToken);
    }

    // ============================================================
    // CANCEL ORDER
    // ============================================================

    public async Task<PaymentResponse?> CancelAsync(
        int tenantId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .Include(p => p.Appointment)
            .FirstOrDefaultAsync(
                p => p.PaymentId == paymentId,
                cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.Appointment?.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                "You can only cancel your own payments.");
        }

        if (!PaymentActionRules.CanCancel(payment.PaymentStatus))
        {
            throw new InvalidOperationException(
                $"A payment in status '{payment.PaymentStatus}' cannot be " +
                "cancelled.");
        }

        payment.PaymentStatus = PaymentStatuses.Cancelled;

        // CK_Payments_FailureData requires FailureReason to be NULL for any
        // status other than Failed.
        payment.FailureReason = null;
        payment.UpdatedAt = DateTimeExtensions.UtcNowToSecond();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new InvalidOperationException(
                "The payment order could not be cancelled.",
                exception);
        }

        return await BuildResponseAsync(
            payment.PaymentId,
            tenantId,
            cancellationToken);
    }

    // ============================================================
    // PROJECTION / MAPPING
    // ============================================================

    private async Task<PaymentResponse?> BuildResponseAsync(
        int paymentId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var row = await QueryProjection()
            .FirstOrDefaultAsync(
                row => row.PaymentId == paymentId,
                cancellationToken);

        if (row is null || row.TenantId != tenantId)
        {
            return null;
        }

        return Map(row);
    }

    private IQueryable<PaymentRow> QueryProjection()
    {
        return
            from payment in _context.Payments.AsNoTracking()
            join appointment in _context.Appointments.AsNoTracking()
                on payment.AppointmentId equals appointment.AppointmentId
            join listing in _context.Listings.AsNoTracking()
                on appointment.ListingId equals listing.ListingId
            select new PaymentRow
            {
                PaymentId = payment.PaymentId,
                AppointmentId = appointment.AppointmentId,
                AppointmentStatus = appointment.Status,
                TenantId = appointment.TenantId,
                ListingId = listing.ListingId,
                ListingTitle = listing.Title,
                ListingStatus = listing.Status,
                Amount = payment.Amount,
                PaymentPurpose = payment.PaymentPurpose,
                PaymentMethod = payment.PaymentMethod,
                PaymentStatus = payment.PaymentStatus,
                TransactionCode = payment.TransactionCode,
                FailureReason = payment.FailureReason,
                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt,
                UpdatedAt = payment.UpdatedAt
            };
    }

    private static PaymentResponse Map(PaymentRow row)
    {
        return new PaymentResponse
        {
            PaymentId = row.PaymentId,
            AppointmentId = row.AppointmentId,
            AppointmentStatus = row.AppointmentStatus,
            ListingId = row.ListingId,
            ListingTitle = row.ListingTitle,
            ListingStatus = row.ListingStatus,
            Amount = row.Amount,
            PaymentPurpose = row.PaymentPurpose,
            PaymentMethod = row.PaymentMethod,
            PaymentStatus = row.PaymentStatus,
            TransactionCode = row.TransactionCode,
            FailureReason = row.FailureReason,
            CreatedAt = row.CreatedAt.AsUtc(),
            PaidAt = row.PaidAt.AsUtc(),
            UpdatedAt = row.UpdatedAt.AsUtc(),
            CanPay = PaymentActionRules.CanPay(
                row.PaymentStatus,
                row.AppointmentStatus),
            CanRetry = PaymentActionRules.CanRetry(row.PaymentStatus),
            CanCancel = PaymentActionRules.CanCancel(row.PaymentStatus),

            // The mock gateway screen lives on the client; this path is a
            // convention the Flutter app routes to.
            CheckoutUrl = row.PaymentStatus == PaymentStatuses.Pending
                ? $"roomrum://checkout/{row.PaymentId}"
                : null
        };
    }

    /// <summary>
    /// Stand-in for a real gateway reference. VNPay uses an 8-digit vnp_TransactionNo
    /// plus the merchant's vnp_TxnRef; the mock keeps the same character set so
    /// the column contract does not change when a real provider is wired in.
    /// </summary>
    private static string GenerateTransactionCode()
    {
        return $"RR{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

    private sealed class PaymentRow
    {
        public int PaymentId { get; set; }

        public int AppointmentId { get; set; }

        public string AppointmentStatus { get; set; } = string.Empty;

        public int TenantId { get; set; }

        public int ListingId { get; set; }

        public string ListingTitle { get; set; } = string.Empty;

        public string ListingStatus { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string PaymentPurpose { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public string? TransactionCode { get; set; }

        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
