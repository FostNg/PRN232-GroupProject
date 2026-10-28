using System.Text.Json.Serialization;

namespace RoomRum.BLL.DTOs.Requests;

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? Dob { get; set; }
    public string? Gender { get; set; }
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}
