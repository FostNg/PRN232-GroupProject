using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index(
    "LandlordId",
    "Status",
    "CreatedAt",
    Name = "IX_Listings_Landlord_Status_Date",
    IsDescending = new[] { false, false, true })]

[Index(
    "Status",
    "City",
    "District",
    "Price",
    "AreaSqm",
    Name = "IX_Listings_Search")]
public partial class Listing
{
    [Key]
    public int ListingId { get; set; }

    public int LandlordId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [StringLength(250)]
    public string AddressLine { get; set; } = null!;

    [StringLength(100)]
    public string? Ward { get; set; }

    [StringLength(100)]
    public string District { get; set; } = null!;

    [StringLength(100)]
    public string City { get; set; } = null!;

    [Column(TypeName = "decimal(18, 0)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal DepositAmount { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal AreaSqm { get; set; }

    public byte Bedrooms { get; set; }

    public byte Bathrooms { get; set; }

    public bool HasWifi { get; set; }

    public bool HasAirConditioner { get; set; }

    public bool HasParking { get; set; }

    public bool HasKitchen { get; set; }

    public bool HasPrivateBathroom { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal? ElectricityPrice { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal? WaterPrice { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal? ServiceFee { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    // ============================================================
    // REVIEW (v5.2)
    // PendingReview / Approved / Rejected. Only an Approved listing may be
    // moved to Active; see ListingService and /api/Admin/listings.
    // ============================================================

    [StringLength(20)]
    public string ReviewStatus { get; set; } = null!;

    public int? ReviewedByUserId { get; set; }

    [Precision(0)]
    public DateTime? ReviewedAt { get; set; }

    [StringLength(500)]
    public string? RejectReason { get; set; }

    [StringLength(500)]
    public string? ReviewComment { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    // ============================================================
    // APPOINTMENTS
    // One Listing can have many Appointments.
    // ============================================================

    [InverseProperty("Listing")]
    public virtual ICollection<Appointment> Appointments { get; set; }
        = new List<Appointment>();

    // ============================================================
    // CONVERSATIONS
    // One Listing can have many Conversations.
    // ============================================================

    [InverseProperty("Listing")]
    public virtual ICollection<Conversation> Conversations { get; set; }
        = new List<Conversation>();

    // ============================================================
    // FAVORITES
    // One Listing can have many Favorites.
    // ============================================================

    [InverseProperty("Listing")]
    public virtual ICollection<Favorite> Favorites { get; set; }
        = new List<Favorite>();

    // ============================================================
    // LANDLORD
    // ============================================================

    [ForeignKey("LandlordId")]
    [InverseProperty("Listings")]
    public virtual User Landlord { get; set; } = null!;

    // ============================================================
    // REVIEWER
    // The Admin who approved/rejected this listing. NULL on legacy rows and
    // while the listing is PendingReview.
    // ============================================================

    [ForeignKey("ReviewedByUserId")]
    [InverseProperty("ReviewedListings")]
    public virtual User? Reviewer { get; set; }

    // ============================================================
    // LISTING IMAGES
    // One Listing can have many ListingImages.
    // ============================================================

    [InverseProperty("Listing")]
    public virtual ICollection<ListingImage> ListingImages { get; set; }
        = new List<ListingImage>();

    public virtual ICollection<ListingVideo> ListingVideos { get; set; }
        = new List<ListingVideo>();
}
