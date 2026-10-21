using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

public class ListingVideo
{
    [Key]
    public int VideoId { get; set; }

    public int ListingId { get; set; }

    [Column(TypeName = "varbinary(max)")]
    public byte[] VideoData { get; set; } = [];

    [StringLength(100)]
    public string ContentType { get; set; } = "video/mp4";

    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;
}
