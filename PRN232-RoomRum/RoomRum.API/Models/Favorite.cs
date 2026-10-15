using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[PrimaryKey("TenantId", "ListingId")]
[Index("TenantId", "CreatedAt", Name = "IX_Favorites_Tenant_Date", IsDescending = new[] { false, true })]
public partial class Favorite
{
    [Key]
    public int TenantId { get; set; }

    [Key]
    public int ListingId { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ListingId")]
    [InverseProperty("Favorites")]
    public virtual Listing Listing { get; set; } = null!;

    [ForeignKey("TenantId")]
    [InverseProperty("Favorites")]
    public virtual User Tenant { get; set; } = null!;
}
