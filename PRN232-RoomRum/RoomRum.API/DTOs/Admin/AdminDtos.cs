using System.ComponentModel.DataAnnotations;

namespace RoomRum.API.DTOs.Admin;

public class RequestListingChangesRequest
{
    public string Comment { get; set; } = string.Empty;
}

public class RejectListingRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Operational counters for the admin dashboard (v5.2).</summary>
public class AdminStatsResponse
{
    // Users
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TenantCount { get; set; }
    public int LandlordCount { get; set; }
    public int AdminCount { get; set; }

    // Listings
    public int TotalListings { get; set; }
    public int ActiveListings { get; set; }
    public int HiddenListings { get; set; }
    public int ReservedListings { get; set; }
    public int RentedListings { get; set; }
    public int DeletedListings { get; set; }

    // Listing review queue
    public int PendingReviewListings { get; set; }
    public int ApprovedListings { get; set; }
    public int RejectedListings { get; set; }

    // Appointments
    public int TotalAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int ConfirmedAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int RejectedAppointments { get; set; }

    // Payments
    public int TotalPayments { get; set; }
    public int PaidPayments { get; set; }

    /// <summary>Sum of every Paid payment amount, in VND.</summary>
    public decimal PaidRevenue { get; set; }

    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
}

/// <summary>A user row in the admin account list.</summary>
public class AdminUserResponse
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int ListingCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AdminUserPagedResponse
{
    public List<AdminUserResponse> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
