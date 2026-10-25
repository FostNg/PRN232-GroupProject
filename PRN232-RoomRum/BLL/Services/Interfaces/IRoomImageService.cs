using RoomRum.BLL.DTOs.Responses;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRoomImageService
{
    Task<ApiOutcome> Upload(Guid roomId, Guid userId, bool isAdmin, Stream input,
        long length, string contentRoot, CancellationToken cancellationToken);
    Task<RoomImageFile?> GetContent(Guid roomId, Guid imageId, string contentRoot);
}
