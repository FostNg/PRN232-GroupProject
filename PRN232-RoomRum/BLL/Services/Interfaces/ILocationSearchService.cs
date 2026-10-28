using RoomRum.BLL.DTOs.Responses;

namespace RoomRum.BLL.Services.Interfaces;

public interface ILocationSearchService
{
    Task<ApiOutcomeResponse> Search(string? query, CancellationToken cancellationToken);
    Task<ApiOutcomeResponse> Suggest(string? query, CancellationToken cancellationToken);
}
