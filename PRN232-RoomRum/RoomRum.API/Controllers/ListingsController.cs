using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRum.API.Common;
using RoomRum.API.Data;
using RoomRum.API.Models;
using RoomRum.API.DTOs.Listings;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListingsController : ApiControllerBase
{
    private const long MaxListingVideoBytes = 100L * 1024 * 1024;
    private readonly IListingService _listingService;
    private readonly RoomRumDbContext _db;

    public ListingsController(IListingService listingService, RoomRumDbContext db)
    {
        _listingService = listingService;
        _db = db;
    }

    // ============================================================
    // GET ALL PUBLIC ACTIVE LISTINGS
    // GET: /api/Listings
    // ============================================================

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetListings(
        [FromQuery] ListingSearchQuery query)
    {
        // Range, sort and pagination validation lives in the service so the
        // rules cannot drift between this endpoint and the radius search.
        var result = await _listingService.GetPublicListingsAsync(query);

        return OkEnvelope(result);
    }

    // ============================================================
    // GET PUBLIC LISTING DETAIL
    // GET: /api/Listings/{listingId}
    // ============================================================

    [HttpGet("{listingId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetListing(int listingId)
    {
        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        // The token is optional here: anonymous tenants browse normally, while
        // an authenticated Landlord additionally sees their own Hidden listing
        // so the pre-publish preview works.
        var viewerUserId = GetCurrentUserId();

        var result =
            await _listingService.GetPublicListingByIdAsync(
                listingId,
                viewerUserId);

        if (result == null)
        {
            return NotFoundEnvelope("Listing not found.");
        }

        return OkEnvelope(result);
    }

    // ============================================================
    // GET CURRENT LANDLORD'S LISTINGS
    // GET: /api/Listings/my
    // ============================================================

    [HttpGet("my")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> GetMyListings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (page < 1)
        {
            return BadRequestEnvelope("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > 50)
        {
            return BadRequestEnvelope("PageSize must be between 1 and 50.");
        }

        var result =
            await _listingService.GetMyListingsAsync(
                landlordId.Value,
                page,
                pageSize);

        return OkEnvelope(result);
    }

    // ============================================================
    // CREATE LISTING
    // POST: /api/Listings
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> CreateListing(
        [FromBody] CreateListingRequest request)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        try
        {
            var result =
                await _listingService.CreateListingAsync(
                    landlordId.Value,
                    request);

            return CreatedEnvelope(result, "Draft saved. Add media, review the details, then submit for Admin review.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // UPDATE LISTING
    // PUT: /api/Listings/{listingId}
    // ============================================================

    [HttpPut("{listingId:int}")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> UpdateListing(
        int listingId,
        [FromBody] UpdateListingRequest request)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        try
        {
            var result =
                await _listingService.UpdateListingAsync(
                    landlordId.Value,
                    listingId,
                    request);

            if (result == null)
            {
                return NotFoundEnvelope("Listing not found.");
            }

            return OkEnvelope(result, "Listing updated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // UPDATE LISTING STATUS
    // PATCH: /api/Listings/{listingId}/status
    // ============================================================

    [HttpPatch("{listingId:int}/status")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> UpdateListingStatus(
        int listingId,
        [FromBody] UpdateListingStatusRequest request)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        try
        {
            var result =
                await _listingService.UpdateListingStatusAsync(
                    landlordId.Value,
                    listingId,
                    request.Status);

            if (result == null)
            {
                return NotFoundEnvelope("Listing not found.");
            }

            return OkEnvelope(result, "Listing status updated successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // ADD IMAGE
    // POST: /api/Listings/{listingId}/images
    // ============================================================

    [HttpPost("{listingId:int}/images")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> AddImage(
        int listingId,
        [FromForm] IFormFile? file,
        [FromForm] bool isPrimary = false,
        [FromForm] int sortOrder = 0)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        if (file == null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return BadRequestEnvelope("Choose an image up to 10 MB.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var bytes = stream.ToArray();
        var contentType = DetectImageType(bytes);
        if (contentType == null)
            return BadRequestEnvelope("Only valid JPEG, PNG, or WebP images are accepted.");

        var listing = await _db.Listings.FirstOrDefaultAsync(x =>
            x.ListingId == listingId && x.LandlordId == landlordId.Value);
        if (listing == null) return NotFoundEnvelope("Listing not found or does not belong to you.");
        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            return ConflictEnvelope("Photos can only be changed on a Draft or a listing returned for changes.");
        if (listing.Status is "Reserved" or "Rented" or "Deleted")
            return ConflictEnvelope("Images cannot be changed for a Reserved, Rented or Deleted listing.");
        var count = await _db.ListingImages.CountAsync(x => x.ListingId == listingId);
        if (count >= 20) return ConflictEnvelope("A listing can have at most 20 images.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);
        var image = new ListingImage {
            ListingId = listingId, Listing = listing, ImageUrl = null,
            ImageData = bytes, ContentType = contentType, IsPrimary = count == 0,
            SortOrder = count == 0 ? 0 : Math.Max(sortOrder, await _db.ListingImages.Where(x => x.ListingId == listingId).MaxAsync(x => x.SortOrder) + 1),
            CreatedAt = DateTimeExtensions.UtcNowToSecond()
        };
        _db.ListingImages.Add(image);
        await _db.SaveChangesAsync();
        image.ImageUrl = $"/api/Listings/{listingId}/images/{image.ImageId}/content";
        await _db.SaveChangesAsync();
        if (isPrimary && count > 0)
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.ListingImages SET IsPrimary = CASE WHEN ImageId = {image.ImageId} THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END WHERE ListingId = {listingId}");
        await tx.CommitAsync();
        return CreatedEnvelope(new ListingImageResponse { ImageId = image.ImageId, ImageUrl = image.ImageUrl!, IsPrimary = image.IsPrimary, SortOrder = image.SortOrder }, "Listing image added successfully.");
    }

    [HttpPost("{listingId:int}/submit")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> SubmitListing(int listingId)
    {
        var landlordId = GetCurrentUserId();
        if (landlordId == null) return MissingIdentity();
        if (listingId <= 0) return BadRequestEnvelope("Invalid listing ID.");
        try
        {
            var result = await _listingService.SubmitListingAsync(landlordId.Value, listingId);
            return result == null
                ? NotFoundEnvelope("Listing not found.")
                : OkEnvelope(result, "Listing submitted for Admin review.");
        }
        catch (ArgumentException ex) { return BadRequestEnvelope(ex.Message); }
        catch (InvalidOperationException ex) { return ConflictEnvelope(ex.Message); }
    }

    [HttpPost("{listingId:int}/video")]
    [Authorize(Roles = "Landlord")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<IActionResult> UploadVideo(int listingId, [FromForm] IFormFile? file)
    {
        var landlordId = GetCurrentUserId();
        if (landlordId == null) return MissingIdentity();
        if (listingId <= 0) return BadRequestEnvelope("Invalid listing ID.");
        if (file == null || file.Length < 12 || file.Length > MaxListingVideoBytes)
            return BadRequestEnvelope("Choose an MP4 video up to 100 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".mp4", StringComparison.OrdinalIgnoreCase))
            return BadRequestEnvelope("Only MP4 videos are accepted.");

        var listing = await _db.Listings.FirstOrDefaultAsync(l =>
            l.ListingId == listingId && l.LandlordId == landlordId.Value);
        if (listing == null) return NotFoundEnvelope("Listing not found or does not belong to you.");
        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            return ConflictEnvelope("Video can only be added to a Draft or a listing returned for changes.");

        await using var stream = new MemoryStream(checked((int)file.Length));
        await file.CopyToAsync(stream);
        var bytes = stream.ToArray();
        if (bytes.Length < 12 || !bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            return BadRequestEnvelope("The selected file is not a valid MP4 container.");

        var video = await _db.ListingVideos.FirstOrDefaultAsync(v => v.ListingId == listingId);
        if (video == null)
        {
            video = new ListingVideo { ListingId = listingId };
            _db.ListingVideos.Add(video);
        }
        video.VideoData = bytes;
        video.ContentType = "video/mp4";
        var safeFileName = Path.GetFileName(file.FileName);
        video.FileName = safeFileName[..Math.Min(safeFileName.Length, 255)];
        video.SizeBytes = bytes.LongLength;
        video.CreatedAt = DateTimeExtensions.UtcNowToSecond();
        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return ConflictEnvelope($"Video could not be stored: {ex.GetBaseException().Message}");
        }

        return OkEnvelope(new ListingVideoResponse
        {
            VideoId = video.VideoId,
            VideoUrl = $"/api/Listings/{listingId}/video/content",
            FileName = video.FileName,
            SizeBytes = video.SizeBytes,
            CreatedAt = video.CreatedAt
        }, "Video stored in the database.");
    }

    [HttpDelete("{listingId:int}/video")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> DeleteVideo(int listingId)
    {
        var landlordId = GetCurrentUserId();
        if (landlordId == null) return MissingIdentity();
        var listing = await _db.Listings.FirstOrDefaultAsync(l =>
            l.ListingId == listingId && l.LandlordId == landlordId.Value);
        if (listing == null) return NotFoundEnvelope("Listing not found or does not belong to you.");
        if (listing.ReviewStatus is not (ReviewStatuses.Draft or ReviewStatuses.ChangesRequested))
            return ConflictEnvelope("Video can only be changed on a Draft or a listing returned for changes.");
        var video = await _db.ListingVideos.FirstOrDefaultAsync(v => v.ListingId == listingId);
        if (video == null) return NotFoundEnvelope("Listing video not found.");
        _db.ListingVideos.Remove(video);
        listing.UpdatedAt = await NextListingEditTimestampAsync(listing);
        await _db.SaveChangesAsync();
        return OkEnvelope<object?>(null, "Video removed.");
    }

    [HttpGet("{listingId:int}/video/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVideoContent(int listingId)
    {
        var row = await _db.ListingVideos.AsNoTracking()
            .Where(v => v.ListingId == listingId)
            .Select(v => new { v.VideoData, v.ContentType, v.FileName, v.Listing.Status, v.Listing.ReviewStatus, v.Listing.LandlordId })
            .FirstOrDefaultAsync();
        if (row == null) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var viewer = GetCurrentUserId();
        var allowed = (row.Status is "Active" or "Reserved") && row.ReviewStatus == ReviewStatuses.Approved
            || (role == "Landlord" && viewer == row.LandlordId)
            || role == "Admin";
        if (!allowed) return NotFound();
        Response.Headers["Accept-Ranges"] = "bytes";
        return File(row.VideoData, row.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{listingId:int}/images/{imageId:int}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImageContent(int listingId, int imageId)
    {
        var row = await _db.ListingImages.AsNoTracking()
            .Where(i => i.ListingId == listingId && i.ImageId == imageId && i.ImageData != null)
            .Select(i => new { i.ImageData, i.ContentType, i.Listing.Status, i.Listing.ReviewStatus, i.Listing.LandlordId })
            .FirstOrDefaultAsync();
        if (row == null) return NotFound();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var viewer = GetCurrentUserId();
        var allowed = (row.Status is "Active" or "Reserved") && row.ReviewStatus == "Approved"
            || (role == "Landlord" && viewer == row.LandlordId)
            || role == "Admin";
        if (!allowed) return NotFound();
        return File(row.ImageData!, row.ContentType ?? "application/octet-stream");
    }

    private static string? DetectImageType(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4e && b[3] == 0x47 && b[4] == 0x0d && b[5] == 0x0a && b[6] == 0x1a && b[7] == 0x0a) return "image/png";
        if (b.Length >= 12 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(b, 8, 4) == "WEBP") return "image/webp";
        return null;
    }

    // ============================================================
    // DELETE IMAGE
    // DELETE: /api/Listings/{listingId}/images/{imageId}
    // ============================================================

    [HttpDelete("{listingId:int}/images/{imageId:int}")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> DeleteImage(
        int listingId,
        int imageId)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (listingId <= 0 || imageId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID or image ID.");
        }

        try
        {
            var result =
                await _listingService.DeleteImageAsync(
                    landlordId.Value,
                    listingId,
                    imageId);

            if (!result)
            {
                return NotFoundEnvelope("Listing or image not found.");
            }

            return OkEnvelope<object>(null!, "Listing image deleted successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // SET PRIMARY IMAGE
    // PUT: /api/Listings/{listingId}/images/{imageId}/primary
    // ============================================================

    [HttpPut("{listingId:int}/images/{imageId:int}/primary")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> SetPrimaryImage(
        int listingId,
        int imageId)
    {
        var landlordId = GetCurrentUserId();

        if (landlordId == null)
        {
            return MissingIdentity();
        }

        if (listingId <= 0 || imageId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID or image ID.");
        }

        try
        {
            var result =
                await _listingService.SetPrimaryImageAsync(
                    landlordId.Value,
                    listingId,
                    imageId);

            if (!result)
            {
                return NotFoundEnvelope("Listing or image not found.");
            }

            return OkEnvelope<object>(null!, "Primary image updated successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // GET CURRENT USER ID FROM JWT
    // ============================================================

    private async Task<DateTime> NextListingEditTimestampAsync(Listing listing)
    {
        var now = DateTimeExtensions.UtcNowToSecond();
        if (listing.ReviewStatus != ReviewStatuses.ChangesRequested)
            return now;

        var requestedAt = await _db.ListingReviewEvents
            .Where(e => e.ListingId == listing.ListingId && e.EventType == "ChangesRequested")
            .OrderByDescending(e => e.ReviewEventId)
            .Select(e => (DateTime?)e.CreatedAt)
            .FirstOrDefaultAsync();

        return requestedAt.HasValue && now <= requestedAt.Value
            ? requestedAt.Value.AddSeconds(1)
            : now;
    }

    private int? GetCurrentUserId()
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return null;
        }

        return userId;
    }
}
