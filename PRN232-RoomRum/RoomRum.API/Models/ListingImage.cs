using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index(
    "ListingId",
    "IsPrimary",
    "SortOrder",
    "ImageId",
    Name = "IX_ListingImages_Listing_Order",
    IsDescending = new[] { false, true, false, false })]

[Index(
    "ListingId",
    "SortOrder",
    Name = "UQ_ListingImages_Listing_SortOrder",
    IsUnique = true)]
public partial class ListingImage
{
    [Key]
    public int ImageId { get; set; }

    public int ListingId { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    public string? SourceUrl { get; set; }

    [Column(TypeName = "varbinary(max)")]
    public byte[]? ImageData { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    // ============================================================
    // LISTING
    // Many ListingImages belong to one Listing.
    // ============================================================

    [ForeignKey("ListingId")]
    [InverseProperty("ListingImages")]
    public virtual Listing Listing { get; set; } = null!;
}
