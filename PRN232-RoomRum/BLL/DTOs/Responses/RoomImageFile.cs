namespace RoomRum.BLL.DTOs.Responses;

public class RoomImageFile
{
    public string Path { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    public RoomImageFile(string path, string contentType)
    {
        Path = path;
        ContentType = contentType;
    }
}
