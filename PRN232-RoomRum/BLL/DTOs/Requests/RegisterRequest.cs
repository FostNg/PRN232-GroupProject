using System.Text.Json.Serialization;

namespace RoomRum.BLL.DTOs.Requests;

public sealed record RegisterRequest(
    string Name, string Email, string Password, string Role,
    string? Phone = null, DateOnly? Dob = null, string? Gender = null,
    [property: JsonPropertyName("image_url")] string? ImageUrl = null);
