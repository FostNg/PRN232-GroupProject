namespace RoomRum.API.Common;

/// <summary>
/// Role names accepted by dbo.Users.CK_Users_Role.
/// Admin is a system role only; it has no mobile UI in the 5-week MVP.
/// </summary>
public static class UserRoles
{
    public const string Tenant = "Tenant";
    public const string Landlord = "Landlord";
    public const string Admin = "Admin";
}

/// <summary>
/// Listing lifecycle enforced by dbo.TR_Listings_Integrity:
/// Hidden -> Active -> Reserved -> Rented -> Deleted,
/// Active -> Hidden, Hidden -> Deleted.
/// </summary>
public static class ListingStatuses
{
    public const string Hidden = "Hidden";
    public const string Active = "Active";
    public const string Reserved = "Reserved";
    public const string Rented = "Rented";
    public const string Deleted = "Deleted";

    /// <summary>Every status accepted by CK_Listings_Status.</summary>
    public static readonly string[] All =
        [Hidden, Active, Reserved, Rented, Deleted];

    /// <summary>Statuses that may be reached by a Landlord through the API.</summary>
    public static readonly string[] LandlordEditable =
        [Active, Hidden, Deleted];

    /// <summary>Statuses a Tenant can still browse.</summary>
    public static readonly string[] PubliclyVisible =
        [Active, Reserved];

    /// <summary>Statuses where listing content and images are frozen.</summary>
    public static readonly string[] Frozen =
        [Reserved, Rented, Deleted];
}

/// <summary>
/// Listing review workflow (v5.4). Landlords save Drafts and explicitly submit
/// them; admins approve/publish, request changes, or reject submissions.
/// </summary>
public static class ReviewStatuses
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string ChangesRequested = "ChangesRequested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Draft, PendingReview, ChangesRequested, Approved, Rejected];

    public static bool IsValid(string status)
    {
        return All.Contains(status, StringComparer.Ordinal);
    }
}

/// <summary>
/// Appointment lifecycle enforced by dbo.TR_Appointments_Integrity.
/// Pending -> Confirmed -> Completed, Pending -> Rejected / Cancelled.
/// </summary>
public static class AppointmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";

    /// <summary>Statuses that block hiding or deleting the parent listing.</summary>
    public static readonly string[] Unresolved = [Pending, Confirmed];
}

/// <summary>
/// Payment lifecycle enforced by dbo.TR_Payments_Integrity.
/// Pending -> Paid (terminal) / Failed -> Pending (retry) / Cancelled.
/// </summary>
public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

/// <summary>Values accepted by dbo.Payments.CK_Payments_Method.</summary>
public static class PaymentMethods
{
    public const string BankTransfer = "BankTransfer";
    public const string VNPay = "VNPay";

    public static readonly string[] All = [BankTransfer, VNPay];
}

/// <summary>Values accepted by dbo.Payments.CK_Payments_Purpose.</summary>
public static class PaymentPurposes
{
    public const string BookingDeposit = "BookingDeposit";
}

/// <summary>
/// Which appointment status a role may move an appointment into, for each
/// current status. Mirrors dbo.TR_Appointments_Integrity so the mobile app can
/// render the correct buttons without duplicating the state machine in Dart.
/// </summary>
public static class AppointmentStatusRules
{
    public static IReadOnlyList<string> AllowedTransitions(
        string currentStatus,
        string role)
    {
        return (currentStatus, role) switch
        {
            (AppointmentStatuses.Pending, UserRoles.Landlord) =>
                [AppointmentStatuses.Confirmed, AppointmentStatuses.Rejected],

            (AppointmentStatuses.Pending, UserRoles.Tenant) =>
                [AppointmentStatuses.Cancelled],

            (AppointmentStatuses.Confirmed, UserRoles.Landlord) =>
                [AppointmentStatuses.Completed],

            (AppointmentStatuses.Confirmed, UserRoles.Tenant) =>
                [AppointmentStatuses.Cancelled],

            _ => []
        };
    }

    public static bool IsTransitionAllowed(
        string currentStatus,
        string role,
        string targetStatus)
    {
        return AllowedTransitions(currentStatus, role)
            .Contains(targetStatus, StringComparer.Ordinal);
    }
}

/// <summary>
/// Which payment action a Tenant may perform, given the payment and the
/// appointment it belongs to. Mirrors dbo.TR_Payments_Integrity.
/// </summary>
public static class PaymentActionRules
{
    public static bool CanPay(string paymentStatus, string appointmentStatus)
    {
        return paymentStatus == PaymentStatuses.Pending &&
               appointmentStatus == AppointmentStatuses.Confirmed;
    }

    public static bool CanRetry(string paymentStatus)
    {
        return paymentStatus == PaymentStatuses.Failed;
    }

    public static bool CanCancel(string paymentStatus)
    {
        return paymentStatus is PaymentStatuses.Pending or PaymentStatuses.Failed;
    }
}
