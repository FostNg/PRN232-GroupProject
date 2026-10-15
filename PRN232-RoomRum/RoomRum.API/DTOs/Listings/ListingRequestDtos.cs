using System.ComponentModel.DataAnnotations;

namespace RoomRum.API.DTOs.Listings;

public class CreateListingRequest
{
    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000, MinimumLength = 10)]
    public string? Description { get; set; }

    [Required]
    [StringLength(250, MinimumLength = 5)]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 2)]
    public string? Ward { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string District { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Range(1, 999999999999999999)]
    public decimal Price { get; set; }

    [Range(1, 999999999999999999)]
    public decimal DepositAmount { get; set; }

    [Range(0.01, 999999.99)]
    public decimal AreaSqm { get; set; }

    [Range(0, 20)]
    public byte Bedrooms { get; set; }

    [Range(1, 20)]
    public byte Bathrooms { get; set; } = 1;

    public bool HasWifi { get; set; }

    public bool HasAirConditioner { get; set; }

    public bool HasParking { get; set; }

    public bool HasKitchen { get; set; }

    public bool HasPrivateBathroom { get; set; } = true;

    [Range(0, 999999999999999999)]
    public decimal? ElectricityPrice { get; set; }

    [Range(0, 999999999999999999)]
    public decimal? WaterPrice { get; set; }

    [Range(0, 999999999999999999)]
    public decimal? ServiceFee { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

}

public class UpdateListingRequest
{
    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000, MinimumLength = 10)]
    public string? Description { get; set; }

    [Required]
    [StringLength(250, MinimumLength = 5)]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 2)]
    public string? Ward { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string District { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Range(1, 999999999999999999)]
    public decimal Price { get; set; }

    [Range(1, 999999999999999999)]
    public decimal DepositAmount { get; set; }

    [Range(0.01, 999999.99)]
    public decimal AreaSqm { get; set; }

    [Range(0, 20)]
    public byte Bedrooms { get; set; }

    [Range(1, 20)]
    public byte Bathrooms { get; set; } = 1;

    public bool HasWifi { get; set; }

    public bool HasAirConditioner { get; set; }

    public bool HasParking { get; set; }

    public bool HasKitchen { get; set; }

    public bool HasPrivateBathroom { get; set; } = true;

    [Range(0, 999999999999999999)]
    public decimal? ElectricityPrice { get; set; }

    [Range(0, 999999999999999999)]
    public decimal? WaterPrice { get; set; }

    [Range(0, 999999999999999999)]
    public decimal? ServiceFee { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}

public class UpdateListingStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}
