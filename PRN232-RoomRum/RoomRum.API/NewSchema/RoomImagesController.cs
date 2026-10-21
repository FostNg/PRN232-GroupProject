using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL;
using RoomRum.DAL.Models;

namespace RoomRum.API.NewSchema;

[Route("api/rooms/{roomId:guid}/images")]
public sealed class RoomImagesController(NewRoomRumDbContext db, IWebHostEnvironment environment) : ApiControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpPost]
    [Authorize(Roles = "Landlord,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid roomId, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length < 12 || file.Length > MaxImageBytes)
            return BadRequest("Choose a JPEG, PNG, or WebP image up to 5 MB.");

        var ownerId = await db.Rooms.AsNoTracking().Where(room => room.RoomId == roomId)
            .Select(room => (Guid?)room.UserId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) return NotFound();
        if (ownerId != UserId && !User.IsInRole("Admin")) return Forbid();
        if (await db.RoomImages.CountAsync(image => image.RoomId == roomId, cancellationToken) >= 20)
            return Conflict("A room can have at most 20 images.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        await input.ReadExactlyAsync(header, cancellationToken);
        var extension = DetectExtension(header);
        if (extension is null) return BadRequest("Only valid JPEG, PNG, or WebP images are accepted.");

        var imageId = Guid.NewGuid();
        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "room-images", roomId.ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, imageId.ToString("N") + extension);
        try
        {
            input.Position = 0;
            await using (var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);

            var imageUrl = $"/api/rooms/{roomId}/images/{imageId}/content";
            db.RoomImages.Add(new RoomImage { RoomImageId = imageId, RoomId = roomId, ImageUrl = imageUrl });
            await db.SaveChangesAsync(cancellationToken);
            return CreatedData(new { roomImageId = imageId, imageUrl });
        }
        catch
        {
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            throw;
        }
    }

    [HttpGet("{imageId:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> Content(Guid roomId, Guid imageId, CancellationToken cancellationToken)
    {
        var expectedUrl = $"/api/rooms/{roomId}/images/{imageId}/content";
        var exists = await db.RoomImages.AsNoTracking().AnyAsync(image =>
            image.RoomId == roomId && image.RoomImageId == imageId && image.ImageUrl == expectedUrl,
            cancellationToken);
        if (!exists) return NotFound();

        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "room-images", roomId.ToString("N"));
        foreach (var (extension, contentType) in new[]
                 { (".jpg", "image/jpeg"), (".png", "image/png"), (".webp", "image/webp") })
        {
            var filePath = Path.Combine(directory, imageId.ToString("N") + extension);
            if (System.IO.File.Exists(filePath))
                return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
        }
        return NotFound();
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
