namespace RoomRum.API.DTOs.Listings;

public class ListingImageResponse
{
    public int ImageId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }
}

public class ListingVideoResponse
{
    public int VideoId { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ListingReviewEventResponse
{
    public long ReviewEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ListingResponse
{
    public int ListingId { get; set; }

    public int LandlordId { get; set; }

    public string LandlordName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string AddressLine { get; set; } = string.Empty;

    public string? Ward { get; set; }

    public string District { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal AreaSqm { get; set; }

    public byte Bedrooms { get; set; }

    public byte Bathrooms { get; set; }

    public bool HasWifi { get; set; }

    public bool HasAirConditioner { get; set; }

    public bool HasParking { get; set; }

    public bool HasKitchen { get; set; }

    public bool HasPrivateBathroom { get; set; }

    public decimal? ElectricityPrice { get; set; }

    public decimal? WaterPrice { get; set; }

    public decimal? ServiceFee { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string Status { get; set; } = string.Empty;

    // ============================================================
    // REVIEW (v5.2)
    // PendingReview / Approved / Rejected. A landlord sees why a listing is
    // waiting or was refused; admins see the queue through /api/Admin.
    // ============================================================

    public string ReviewStatus { get; set; } = string.Empty;

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectReason { get; set; }

    public string? ReviewComment { get; set; }

    /// <summary>
    /// True when a Tenant can still book or favourite this listing. Reserved
    /// listings remain in search results and on the map, but are not available.
    /// </summary>
    public bool IsAvailable => Status == "Active";

    /// <summary>
    /// Distance from the search centre in kilometres. Only populated by a
    /// radius search (?radiusKm=...), otherwise null.
    /// </summary>
    public double? DistanceKm { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<ListingImageResponse> Images { get; set; } = [];

    public ListingVideoResponse? Video { get; set; }

    public List<ListingReviewEventResponse> ReviewHistory { get; set; } = [];
}

public class ListingPagedResponse
{
    public List<ListingResponse> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
