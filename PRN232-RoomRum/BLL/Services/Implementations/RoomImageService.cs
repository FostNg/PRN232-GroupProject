using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class RoomImageService(IRoomRepository rooms) : IRoomImageService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    public async Task<ApiOutcomeResponse> Upload(Guid roomId, Guid userId, bool isAdmin,
        Stream input, long length, string contentRoot, CancellationToken cancellationToken)
    {
        if (length < 12 || length > MaxImageBytes)
            return ApiOutcomeResponse.BadRequest("Choose a JPEG, PNG, or WebP image up to 5 MB.");
        var ownerId = await rooms.GetOwnerIdAsync(roomId);
        if (ownerId is null) return ApiOutcomeResponse.NotFound();
        if (ownerId != userId && !isAdmin) return ApiOutcomeResponse.Forbidden();
        if (await rooms.CountImagesAsync(roomId) >= 20)
            return ApiOutcomeResponse.Conflict("A room can have at most 20 images.");

        var header = new byte[12];
        await input.ReadExactlyAsync(header, cancellationToken);
        var extension = DetectExtension(header);
        if (extension is null)
            return ApiOutcomeResponse.BadRequest("Only valid JPEG, PNG, or WebP images are accepted.");

        var imageId = Guid.NewGuid();
        var directory = Path.Combine(contentRoot, "App_Data", "room-images", roomId.ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, imageId.ToString("N") + extension);
        try
        {
            await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await output.WriteAsync(header, cancellationToken);
                await input.CopyToAsync(output, cancellationToken);
            }
            var imageUrl = $"/api/rooms/{roomId}/images/{imageId}/content";
            rooms.AddImage(new RoomImage { RoomImageId = imageId, RoomId = roomId, ImageUrl = imageUrl });
            await rooms.SaveChangesAsync();
            return ApiOutcomeResponse.Created(new RoomImageResponse { RoomImageId = imageId, ImageUrl = imageUrl });
        }
        catch
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            throw;
        }
    }

    public async Task<RoomImageFile?> GetContent(Guid roomId, Guid imageId, string contentRoot)
    {
        var expectedUrl = $"/api/rooms/{roomId}/images/{imageId}/content";
        if (!await rooms.ImageExistsAsync(roomId, imageId, expectedUrl)) return null;
        var directory = Path.Combine(contentRoot, "App_Data", "room-images", roomId.ToString("N"));
        foreach (var (extension, contentType) in new[]
                 { (".jpg", "image/jpeg"), (".png", "image/png"), (".webp", "image/webp") })
        {
            var path = Path.Combine(directory, imageId.ToString("N") + extension);
            if (File.Exists(path)) return new RoomImageFile(path, contentType);
        }
        return null;
    }

    private static string? DetectExtension(byte[] bytes)
    {
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}
