using RoomRum.BLL.DTOs.Responses;

namespace RoomRum.BLL.Services.Interfaces;

public interface ILocationSearchService
{
    Task<ApiOutcome> Search(string? query, CancellationToken cancellationToken);
    Task<ApiOutcome> Suggest(string? query, CancellationToken cancellationToken);
}
