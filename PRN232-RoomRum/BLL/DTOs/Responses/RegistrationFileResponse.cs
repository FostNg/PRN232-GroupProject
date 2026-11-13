namespace RoomRum.BLL.DTOs.Responses;

public sealed class RegistrationFileResponse
{
    public Guid FileId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
