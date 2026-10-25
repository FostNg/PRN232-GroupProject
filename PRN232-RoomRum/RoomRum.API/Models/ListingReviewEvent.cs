using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

public class ListingReviewEvent
{
    [Key]
    public long ReviewEventId { get; set; }

    public int ListingId { get; set; }

    public int ActorUserId { get; set; }

    [StringLength(30)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Comment { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }
}
