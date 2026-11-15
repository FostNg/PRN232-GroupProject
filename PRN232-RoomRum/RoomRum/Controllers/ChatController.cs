using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.API;
using RoomRum.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace RoomRum.API.Controllers;

[Authorize]
[Route("api/chat")]
public sealed class ChatController(IChatService chat, IHubContext<ChatHub> hub,
    ILogger<ChatController> logger) : ApiControllerBase
{
    [HttpGet("unanswered-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnansweredCount() => Respond(await chat.UnansweredCount(UserId));

    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<List<ConversationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await chat.Mine(UserId, page, pageSize));

    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Messages(Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Respond(await chat.Messages(UserId, id, page, pageSize));

    [HttpPost("messages")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(MessageRequest request)
    {
        var result = await chat.Send(UserId, request);
        if (result.StatusCode == StatusCodes.Status201Created)
        {
            try
            {
                await hub.Clients.Users(UserId.ToString(), request.Receiver.ToString())
                    .SendAsync("ChatChanged");
            }
            catch (Exception error)
            {
                logger.LogError(error, "Chat notification failed after saving a message.");
            }
        }
        return RespondWrite(result, "Đã gửi tin nhắn.");
    }
}
