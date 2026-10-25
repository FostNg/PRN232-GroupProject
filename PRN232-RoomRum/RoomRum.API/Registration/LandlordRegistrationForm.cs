namespace RoomRum.API.Registration;

public sealed class LandlordRegistrationForm
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public List<IFormFile> PropertyImages { get; set; } = [];
    public List<IFormFile> IdentityDocuments { get; set; } = [];
}
