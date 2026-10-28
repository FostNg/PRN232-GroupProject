using System.Text;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services;

public static class RegistrationFileValidator
{
    public const int MaxFileBytes = 5 * 1024 * 1024;

    public static RegistrationFile? Validate(string category, string fileName, byte[] bytes)
    {
        if (bytes.Length is < 12 or > MaxFileBytes) return null;
        // Reuse the legacy ListingsController's byte-signature checks, with extension matching.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        string? contentType = null;
        if (bytes.AsSpan(0, 3).SequenceEqual(new byte[] { 0xff, 0xd8, 0xff }) && extension is ".jpg" or ".jpeg")
            contentType = "image/jpeg";
        else if (bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) && extension == ".png")
            contentType = "image/png";
        else if (Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" && extension == ".webp")
            contentType = "image/webp";
        else if (category == "IdentityDocument" && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8) && extension == ".pdf")
            contentType = "application/pdf";
        var safeName = Path.GetFileName(fileName);
        return contentType is null || string.IsNullOrWhiteSpace(safeName) || safeName.Length > 255
            ? null : new RegistrationFile
            {
                Category = category,
                FileName = safeName,
                ContentType = contentType,
                Content = bytes
            };
    }
}

