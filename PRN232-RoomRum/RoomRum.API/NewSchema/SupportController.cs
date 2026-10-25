using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Authorize]
[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<ReportSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await reports.Mine(UserId, User.IsInRole("Admin"), page, pageSize));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReportCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ReportRequest request) => Respond(await reports.Create(UserId, request));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ReportStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(Guid id, StatusRequest request) => Respond(await reports.Status(id, request));
}

[Authorize]
[Route("api/repairs")]
public sealed class RepairsController(IRepairService repairs) : ApiControllerBase
{
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<List<RepairSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await repairs.Mine(UserId, page, pageSize));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RepairCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(RepairRequest request) => Respond(await repairs.Create(UserId, request));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RepairStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Respond(Guid id, RepairResponseRequest request) =>
        base.Respond(await repairs.Respond(UserId, id, request));
}
