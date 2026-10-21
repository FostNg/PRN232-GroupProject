namespace RoomRum.BLL.DTOs.Responses;

public sealed record LocationSearchResponse(string DisplayName, double Latitude,
    double Longitude, bool IsApproximate);
