namespace RoomRum.API.DTOs.Favorites;

public sealed class FavoriteResponseDto
{
    public int ListingId { get; set; }

    public string Title { get; set; } = null!;

    public decimal Price { get; set; }

    public decimal AreaSqm { get; set; }

    public string AddressLine { get; set; } = null!;

    public string? Ward { get; set; }

    public string District { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool IsAvailable { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public DateTime FavoritedAt { get; set; }
}