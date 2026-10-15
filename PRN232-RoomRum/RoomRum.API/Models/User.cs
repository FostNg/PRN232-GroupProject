using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index("Role", "IsActive", "UserId", Name = "IX_Users_Role_Active")]
[Index("EmailNormalized", Name = "UX_Users_EmailNormalized", IsUnique = true)]
public partial class User
{
    [Key]
    public int UserId { get; set; }

    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [StringLength(150)]
    public string Email { get; set; } = null!;

    [StringLength(150)]
    public string? EmailNormalized { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(20)]
    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    [InverseProperty("Tenant")]
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    [InverseProperty("Landlord")]
    public virtual ICollection<Conversation> ConversationLandlords { get; set; } = new List<Conversation>();

    [InverseProperty("Tenant")]
    public virtual ICollection<Conversation> ConversationTenants { get; set; } = new List<Conversation>();

    [InverseProperty("Tenant")]
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [InverseProperty("Landlord")]
    public virtual ICollection<Listing> Listings { get; set; } = new List<Listing>();

    [InverseProperty("Sender")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [InverseProperty("User")]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    /// <summary>Listings this Admin has reviewed (TR_Listings_ReviewIntegrity).</summary>
    [InverseProperty("Reviewer")]
    public virtual ICollection<Listing> ReviewedListings { get; set; } = new List<Listing>();
}
