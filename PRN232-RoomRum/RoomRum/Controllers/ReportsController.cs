using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;

namespace RoomRum.API.Controllers;

[Authorize]
[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<ReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await reports.Mine(UserId, User.IsInRole("Admin"), page, pageSize));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ReportRequest request) =>
        RespondWrite(await reports.Create(UserId, request), "Đã gửi báo cáo.");

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<ReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pending([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await reports.Pending(page, pageSize));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(Guid id, StatusRequest request) =>
        RespondWrite(await reports.Status(id, request), "Đã cập nhật trạng thái báo cáo.");
}
