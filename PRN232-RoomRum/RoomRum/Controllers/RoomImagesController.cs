using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Interfaces;

namespace RoomRum.API.Controllers;

[Route("api/rooms/{roomId:guid}/images")]
public sealed class RoomImagesController(IRoomImageService images,
    IWebHostEnvironment environment) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Landlord,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RoomImageResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(Guid roomId, [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest("Choose a JPEG, PNG, or WebP image up to 5 MB.");
        await using var input = file.OpenReadStream();
        return Respond(await images.Upload(roomId, UserId, User.IsInRole("Admin"), input,
            file.Length, environment.ContentRootPath, cancellationToken));
    }

    [HttpGet("{imageId:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> Content(Guid roomId, Guid imageId)
    {
        var image = await images.GetContent(roomId, imageId, environment.ContentRootPath);
        return image is null ? NotFound() : PhysicalFile(image.Path, image.ContentType,
            enableRangeProcessing: true);
    }
}
