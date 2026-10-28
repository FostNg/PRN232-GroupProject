using RoomRum.BLL.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL;
using RoomRum.API.Common;

namespace RoomRum.API.NewSchema;

[Authorize]
[Route("api/chat")]
public sealed class ChatController(IChatService chat) : ApiControllerBase
{
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<List<ConversationSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await chat.Mine(UserId, page, pageSize));

    [HttpPost("messages")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageCreatedResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(MessageRequest request) => Respond(await chat.Send(UserId, request));

    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Messages(Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await chat.Messages(UserId, id, page, pageSize));
}
