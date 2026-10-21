using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.DTOs.Requests;

namespace RoomRum.BLL.Services.Interfaces;

public interface IRoomTenantService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Add(Guid userId, Guid roomId, TenantRequest request);
    Task<ApiOutcome> AddByEmail(Guid userId, Guid roomId, TenantInviteByEmailRequest request);
    Task<ApiOutcome> Accept(Guid userId, Guid roomId);
    Task<ApiOutcome> End(Guid userId, Guid roomId, Guid tenantId);
    Task<ApiOutcome> History(Guid userId, Guid roomId, Guid tenantId, int page, int pageSize);
}
