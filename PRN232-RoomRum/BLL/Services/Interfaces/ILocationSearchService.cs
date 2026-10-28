namespace RoomRum.BLL.Services.Interfaces;

using RoomRum.BLL.DTOs.Responses;

public interface ILocationSearchService
{
    Task<ApiOutcome> Search(string? query, CancellationToken cancellationToken);
    Task<ApiOutcome> Suggest(string? query, CancellationToken cancellationToken);
}
