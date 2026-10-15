using RoomRum.DAL;
using RoomRum.BLL.Contracts;


using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.BLL;
public interface IChatService
{
    Task<ApiOutcome> Mine(Guid userId, int page, int pageSize);
    Task<ApiOutcome> Messages(Guid userId, Guid id, int page, int pageSize);
    Task<ApiOutcome> Send(Guid userId, MessageRequest request);
}
public sealed class ChatService(NewRoomRumDbContext db) : IChatService
{
    public async Task<ApiOutcome> Mine(Guid userId, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        var result = await db.Conversations.AsNoTracking()
            .Where(x => x.Messages.Any(m => m.Sender == userId || m.Receiver == userId))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ConversationId)
            .Skip(skip).Take(pageSize)
            .Select(x => new ConversationSummaryResponse(x.ConversationId, x.CreatedAt,
                 x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                     .OrderByDescending(m => m.ChatAt)
                    .Select(m => new ChatLastMessageResponse(m.Content, m.ChatAt)).FirstOrDefault()))
            .ToListAsync();
        return ApiOutcome.Ok(result);
    }
    public async Task<ApiOutcome> Messages(Guid userId, Guid id, int page, int pageSize)
    {
        if (!Paging.TryGetSkip(page, pageSize, out var skip)) return ApiOutcome.BadRequest("Invalid pagination.");
        if (!await db.Messages.AnyAsync(x => x.ConversationId == id && (x.Sender == userId || x.Receiver == userId)))
            return ApiOutcome.NotFound();
        return ApiOutcome.Ok(await db.Messages.AsNoTracking().Where(x => x.ConversationId == id &&
                (x.Sender == userId || x.Receiver == userId))
            .OrderBy(x => x.ChatAt).ThenBy(x => x.MessageId).Skip(skip).Take(pageSize)
            .Select(x => new ChatMessageResponse(x.MessageId, x.ConversationId,
                x.Sender, x.Receiver, x.Content, x.ChatAt))
            .ToListAsync());
    }
    public async Task<ApiOutcome> Send(Guid userId, MessageRequest request)
    {
        if (request.Receiver == userId || string.IsNullOrWhiteSpace(request.Content))
            return ApiOutcome.BadRequest("A different receiver and nonempty content are required.");
        if (!await db.Users.AnyAsync(x => x.UserId == request.Receiver)) return ApiOutcome.NotFound("Receiver not found.");
        Conversation conversation;
        if (request.ConversationId is Guid id)
        {
            conversation = await db.Conversations.FindAsync(id) ?? throw new KeyNotFoundException("Conversation not found.");
            if (!await db.Messages.AnyAsync(x => x.ConversationId == id) ||
                await db.Messages.AnyAsync(x => x.ConversationId == id &&
                    ((x.Sender != userId && x.Sender != request.Receiver) ||
                     (x.Receiver != userId && x.Receiver != request.Receiver))))
                return ApiOutcome.Forbidden();
            if (!await db.Messages.AnyAsync(x => x.ConversationId == id &&
                (x.Sender == userId || x.Receiver == userId))) return ApiOutcome.Forbidden();
        }
        else
        {
            conversation = new Conversation { ConversationId = Guid.NewGuid() };
            db.Conversations.Add(conversation);
        }
        var message = new Message { MessageId = Guid.NewGuid(), ConversationId = conversation.ConversationId,
            Sender = userId, Receiver = request.Receiver, Content = request.Content.Trim() };
        db.Messages.Add(message); await db.SaveChangesAsync();
        return ApiOutcome.Created(new ChatMessageCreatedResponse(message.MessageId, message.ConversationId));
    }
}
