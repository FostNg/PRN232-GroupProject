using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class ChatService(IChatRepository chat, IUserRepository users) : IChatService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        var result = await chat.ListConversationsAsync(userId, skip, pageSize,
            x => new ConversationSummaryResponse(x.ConversationId, x.CreatedAt,
                 x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                     .OrderByDescending(m => m.ChatAt)
                    .Select(m => new ChatLastMessageResponse(m.Content, m.ChatAt)).FirstOrDefault(),
                 x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                     .OrderByDescending(m => m.ChatAt)
                     .Select(m => (Guid?)(m.Sender == userId ? m.Receiver : m.Sender)).FirstOrDefault(),
                 x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                     .OrderByDescending(m => m.ChatAt)
                     .Select(m => m.Sender == userId ? m.ReceiverNavigation.Name : m.SenderNavigation.Name)
                     .FirstOrDefault()));
        return ApiOutcome.Ok(result);
    }
    public async Task<ApiOutcome> Messages(Guid userId, Guid id, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        if (!await chat.HasParticipantAsync(id, userId))
            return ApiOutcome.NotFound();
        return ApiOutcome.Ok(await chat.ListMessagesAsync(id, userId, skip, pageSize,
            x => new ChatMessageResponse(x.MessageId, x.ConversationId,
                x.Sender, x.Receiver, x.Content, x.ChatAt)));
    }
    public async Task<ApiOutcome> Send(Guid userId, MessageRequest request)
    {
        if (request.Receiver == userId || string.IsNullOrWhiteSpace(request.Content))
            return ApiOutcome.BadRequest("A different receiver and nonempty content are required.");
        if (!await users.ExistsAsync(request.Receiver)) return ApiOutcome.NotFound("Receiver not found.");
        Conversation conversation;
        if (request.ConversationId is Guid id)
        {
            conversation = await chat.FindConversationAsync(id) ?? throw new KeyNotFoundException("Conversation not found.");
            if (!await chat.ContainsOnlyParticipantsAsync(id, userId, request.Receiver))
                return ApiOutcome.Forbidden();
            if (!await chat.HasParticipantAsync(id, userId)) return ApiOutcome.Forbidden();
        }
        else
        {
            conversation = new Conversation { ConversationId = Guid.NewGuid() };
            chat.AddConversation(conversation);
        }
        var message = new Message { MessageId = Guid.NewGuid(), ConversationId = conversation.ConversationId,
            Sender = userId, Receiver = request.Receiver, Content = request.Content.Trim() };
        chat.AddMessage(message); await chat.SaveChangesAsync();
        return ApiOutcome.Created(new ChatMessageCreatedResponse(message.MessageId, message.ConversationId));
    }
}
