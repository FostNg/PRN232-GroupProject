using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.API.DTOs.Admin;
using RoomRum.API.Services;

namespace RoomRum.API.Controllers;

/// <summary>
/// Admin API (v5.2). Every endpoint requires a JWT with role = Admin; there is
/// no admin mobile UI yet, so this is the surface a future admin web/mobile
/// client (or Swagger) uses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ============================================================
    // REVIEW QUEUE
    // GET: /api/Admin/listings?reviewStatus=PendingReview
    // ============================================================

    [HttpGet("listings")]
    public async Task<IActionResult> GetListings(
        [FromQuery] string? reviewStatus = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _adminService.GetListingsAsync(
                reviewStatus,
                status,
                page,
                pageSize);

            return OkEnvelope(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
    }

    // ============================================================
    // REVIEW DETAIL
    // GET: /api/Admin/listings/{listingId}
    // ============================================================

    [HttpGet("listings/{listingId:int}")]
    public async Task<IActionResult> GetListing(int listingId)
    {
        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        var result = await _adminService.GetListingAsync(listingId);

        if (result == null)
        {
            return NotFoundEnvelope("Listing not found.");
        }

        return OkEnvelope(result);
    }

    // ============================================================
    // APPROVE
    // POST: /api/Admin/listings/{listingId}/approve
    // ============================================================

    [HttpPost("listings/{listingId:int}/approve")]
    public async Task<IActionResult> ApproveListing(int listingId)
    {
        if (!TryGetUserId(out var adminId))
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        try
        {
            var result = await _adminService.ApproveListingAsync(
                adminId,
                listingId);

            if (result == null)
            {
                return NotFoundEnvelope("Listing not found.");
            }

            return OkEnvelope(
                result,
                "Listing approved and published.");
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }

    // ============================================================
    // REQUEST CHANGES
    // POST: /api/Admin/listings/{listingId}/request-changes
    // ============================================================

    [HttpPost("listings/{listingId:int}/request-changes")]
    public async Task<IActionResult> RequestListingChanges(
        int listingId,
        [FromBody] RequestListingChangesRequest request)
    {
        if (!TryGetUserId(out var adminId)) return MissingIdentity();
        if (listingId <= 0) return BadRequestEnvelope("Invalid listing ID.");
        try
        {
            var result = await _adminService.RequestListingChangesAsync(adminId, listingId, request.Comment);
            if (result == null) return NotFoundEnvelope("Listing not found.");
            return OkEnvelope(result, "Changes requested; the landlord can revise and resubmit this listing.");
        }
        catch (ArgumentException ex) { return BadRequestEnvelope(ex.Message); }
        catch (InvalidOperationException ex) { return ConflictEnvelope(ex.Message); }
    }

    // REJECT
    // POST: /api/Admin/listings/{listingId}/reject
    // ============================================================

    [HttpPost("listings/{listingId:int}/reject")]
    public async Task<IActionResult> RejectListing(
        int listingId,
        [FromBody] RejectListingRequest request)
    {
        if (!TryGetUserId(out var adminId))
        {
            return MissingIdentity();
        }

        if (listingId <= 0)
        {
            return BadRequestEnvelope("Invalid listing ID.");
        }

        try
        {
            var result = await _adminService.RejectListingAsync(
                adminId,
                listingId,
                request.Reason);

            if (result == null)
            {
                return NotFoundEnvelope("Listing not found.");
            }

            return OkEnvelope(
                result,
                "Listing rejected. The landlord sees the reason and can edit " +
                "the listing to send it back for review.");
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
    // STATISTICS
    // GET: /api/Admin/stats
    // ============================================================

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _adminService.GetStatsAsync();

        return OkEnvelope(stats);
    }

    // ============================================================
    // ACCOUNT LIST
    // GET: /api/Admin/users?role=Landlord&isActive=true
    // ============================================================

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _adminService.GetUsersAsync(
                role,
                isActive,
                search,
                page,
                pageSize);

            return OkEnvelope(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequestEnvelope(ex.Message);
        }
    }

    // ============================================================
    // LOCK / UNLOCK
    // POST: /api/Admin/users/{userId}/lock
    // POST: /api/Admin/users/{userId}/unlock
    // ============================================================

    [HttpPost("users/{userId:int}/lock")]
    public Task<IActionResult> LockUser(int userId)
    {
        return SetUserActive(userId, false);
    }

    [HttpPost("users/{userId:int}/unlock")]
    public Task<IActionResult> UnlockUser(int userId)
    {
        return SetUserActive(userId, true);
    }

    private async Task<IActionResult> SetUserActive(
        int userId,
        bool isActive)
    {
        if (userId <= 0)
        {
            return BadRequestEnvelope("Invalid user ID.");
        }

        try
        {
            var result = await _adminService.SetUserActiveAsync(
                userId,
                isActive);

            if (result == null)
            {
                return NotFoundEnvelope("User not found.");
            }

            return OkEnvelope(
                result,
                isActive
                    ? "Account unlocked."
                    : "Account locked. Every refresh token it held was revoked.");
        }
        catch (InvalidOperationException ex)
        {
            return ConflictEnvelope(ex.Message);
        }
    }
}
