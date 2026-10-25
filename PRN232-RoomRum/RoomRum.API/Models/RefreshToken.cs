using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Index("UserId", "CreatedAt", Name = "IX_RefreshTokens_User_CreatedAt", IsDescending = new[] { false, true })]
[Index("TokenHash", Name = "UX_RefreshTokens_TokenHash", IsUnique = true)]
public partial class RefreshToken
{
    [Key]
    public int RefreshTokenId { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 of the opaque token, uppercase hex. The raw value is only ever
    /// returned to the client that logged in; the database stores the hash.
    /// </summary>
    [StringLength(64)]
    public string TokenHash { get; set; } = null!;

    [Precision(0)]
    public DateTime ExpiresAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? RevokedAt { get; set; }

    /// <summary>Set on rotation: the hash of the token that replaced this one.</summary>
    [StringLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    [StringLength(45)]
    public string? CreatedByIp { get; set; }

    [StringLength(45)]
    public string? RevokedByIp { get; set; }

    // ============================================================
    // USER
    // ============================================================

    [ForeignKey("UserId")]
    [InverseProperty("RefreshTokens")]
    public virtual User User { get; set; } = null!;
}
