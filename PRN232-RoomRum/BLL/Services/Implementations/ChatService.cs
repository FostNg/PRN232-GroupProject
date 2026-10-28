using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services;
using RoomRum.BLL.DTOs.Requests;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.BLL.Services.Implementations;

public sealed class ChatService(IChatRepository chat, IUserRepository users) : IChatService
{
    public async Task<ApiOutcomeResponse> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        var result = await chat.ListConversationsAsync(userId, skip, pageSize,
            x => new ConversationResponse
            {
                ConversationId = x.ConversationId,
                CreatedAt = x.CreatedAt,
                LastMessage = x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                    .OrderByDescending(m => m.ChatAt)
                    .Select(m => new ChatLastMessageResponse { Content = m.Content, ChatAt = m.ChatAt })
                    .FirstOrDefault(),
                OtherUserId = x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                    .OrderByDescending(m => m.ChatAt)
                    .Select(m => (Guid?)(m.Sender == userId ? m.Receiver : m.Sender)).FirstOrDefault(),
                OtherUserName = x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                    .OrderByDescending(m => m.ChatAt)
                    .Select(m => m.Sender == userId ? m.ReceiverNavigation.Name : m.SenderNavigation.Name)
                    .FirstOrDefault()
            });
        return ApiOutcomeResponse.Ok(result);
    }
    public async Task<ApiOutcomeResponse> Messages(Guid userId, Guid id, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcomeResponse.BadRequest("Invalid pagination.");
        if (!await chat.HasParticipantAsync(id, userId))
            return ApiOutcomeResponse.NotFound();
        return ApiOutcomeResponse.Ok(await chat.ListMessagesAsync(id, userId, skip, pageSize,
            x => new ChatMessageResponse
            {
                MessageId = x.MessageId,
                ConversationId = x.ConversationId,
                Sender = x.Sender,
                Receiver = x.Receiver,
                Content = x.Content,
                ChatAt = x.ChatAt
            }));
    }
    public async Task<ApiOutcomeResponse> Send(Guid userId, MessageRequest request)
    {
        if (request.Receiver == userId || string.IsNullOrWhiteSpace(request.Content))
            return ApiOutcomeResponse.BadRequest("A different receiver and nonempty content are required.");
        if (!await users.ExistsAsync(request.Receiver)) return ApiOutcomeResponse.NotFound("Receiver not found.");
        Conversation conversation;
        if (request.ConversationId is Guid id)
        {
            conversation = await chat.FindConversationAsync(id) ?? throw new KeyNotFoundException("Conversation not found.");
            if (!await chat.ContainsOnlyParticipantsAsync(id, userId, request.Receiver))
                return ApiOutcomeResponse.Forbidden();
            if (!await chat.HasParticipantAsync(id, userId)) return ApiOutcomeResponse.Forbidden();
        }
        else
        {
            conversation = new Conversation { ConversationId = Guid.NewGuid() };
            chat.AddConversation(conversation);
        }
        var message = new Message { MessageId = Guid.NewGuid(), ConversationId = conversation.ConversationId,
            Sender = userId, Receiver = request.Receiver, Content = request.Content.Trim() };
        chat.AddMessage(message); await chat.SaveChangesAsync();
        return ApiOutcomeResponse.Created();
    }
}
