using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRoomTenantService
{
    Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcomeResponse> Add(Guid userId, Guid roomId, TenantRequest request);
    Task<ApiOutcomeResponse> AddByEmail(Guid userId, Guid roomId, TenantInviteByEmailRequest request);
    Task<ApiOutcomeResponse> Accept(Guid userId, Guid roomId);
    Task<ApiOutcomeResponse> End(Guid userId, Guid roomId, Guid tenantId);
    Task<ApiOutcomeResponse> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize);
}
