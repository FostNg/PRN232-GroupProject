using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index("LandlordId", "LastMessageAt", "ConversationId", Name = "IX_Conversations_Landlord_LastMessage", IsDescending = new[] { false, true, true })]
[Index("TenantId", "LastMessageAt", "ConversationId", Name = "IX_Conversations_Tenant_LastMessage", IsDescending = new[] { false, true, true })]
[Index("TenantId", "ListingId", Name = "UX_Conversations_Tenant_Listing", IsUnique = true)]
public partial class Conversation
{
    [Key]
    public int ConversationId { get; set; }

    public int TenantId { get; set; }

    public int LandlordId { get; set; }

    public int ListingId { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? LastMessageAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    [ForeignKey("LandlordId")]
    [InverseProperty("ConversationLandlords")]
    public virtual User Landlord { get; set; } = null!;

    [ForeignKey("ListingId")]
    [InverseProperty("Conversations")]
    public virtual Listing Listing { get; set; } = null!;

    [InverseProperty("Conversation")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [ForeignKey("TenantId")]
    [InverseProperty("ConversationTenants")]
    public virtual User Tenant { get; set; } = null!;
}
