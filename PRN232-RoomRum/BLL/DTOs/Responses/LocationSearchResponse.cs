namespace RoomRum.BLL.DTOs.Responses;

public class LocationSearchResponse
{
    public string DisplayName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsApproximate { get; set; }
}
