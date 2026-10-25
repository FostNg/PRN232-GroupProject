namespace RoomRum.API.DTOs.Listings;

/// <summary>
/// Sort keys accepted by GET /api/Listings?sort=...
/// </summary>
public static class ListingSortOptions
{
    public const string Newest = "newest";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string AreaAsc = "area_asc";
    public const string AreaDesc = "area_desc";
    public const string DepositAsc = "deposit_asc";
    public const string DepositDesc = "deposit_desc";
    public const string AmenitiesDesc = "amenities_desc";

    public static readonly string[] All =
        [Newest, PriceAsc, PriceDesc, AreaAsc, AreaDesc, DepositAsc, DepositDesc, AmenitiesDesc];

    public static bool IsValid(string value)
    {
        return All.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Every filter accepted by the public listing search.
///
/// Bundled into one object so the service signature stays readable and the
/// controller can validate once. All fields are optional; defaults reproduce
/// the original behaviour (newest first, page 1, 10 items).
/// </summary>
public sealed class ListingSearchQuery
{
    // ---------------- Text / location filters ----------------

    /// <summary>Free text matched against title, description and address.</summary>
    public string? Search { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public string? Ward { get; set; }

    // ---------------- Numeric ranges ----------------

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MinArea { get; set; }

    public decimal? MaxArea { get; set; }

    public byte? MinBedrooms { get; set; }

    public byte? MinBathrooms { get; set; }

    // ---------------- Amenity filters ----------------

    /// <summary>
    /// When true only listings with the amenity are returned; when null the
    /// amenity is ignored. False is treated the same as null because a listing
    /// that lacks a feature is simply not filtered out by it.
    /// </summary>
    public bool? HasWifi { get; set; }

    public bool? HasAirConditioner { get; set; }

    public bool? HasParking { get; set; }

    public bool? HasKitchen { get; set; }

    public bool? HasPrivateBathroom { get; set; }

    // ---------------- Radius search (map screen) ----------------

    /// <summary>Centre latitude for a "near me" search.</summary>
    public decimal? Latitude { get; set; }

    /// <summary>Centre longitude for a "near me" search.</summary>
    public decimal? Longitude { get; set; }

    /// <summary>Radius in kilometres; requires Latitude and Longitude.</summary>
    public decimal? RadiusKm { get; set; }

    // ---------------- Paging / sorting ----------------

    public string Sort { get; set; } = ListingSortOptions.Newest;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// True when a radius search was requested. The service falls back to a
    /// bounding-box prefilter, so the caller must supply a centre point.
    /// </summary>
    public bool HasRadiusSearch =>
        Latitude.HasValue &&
        Longitude.HasValue &&
        RadiusKm.HasValue &&
        RadiusKm.Value > 0;

    public decimal CenterLatitude => Latitude ?? 0m;

    public decimal CenterLongitude => Longitude ?? 0m;
}
