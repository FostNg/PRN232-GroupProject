using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class ChatRepository(RoomRumDbContext db) : IChatRepository
{
    public Task<List<T>> ListConversationsAsync<T>(Guid userId, int skip, int take, Expression<Func<Conversation, T>> selector) =>
        db.Conversations.AsNoTracking().Where(x => x.Messages.Any(m => m.Sender == userId || m.Receiver == userId))
            .OrderByDescending(x => x.Messages.Where(m => m.Sender == userId || m.Receiver == userId)
                .Max(m => m.ChatAt)).ThenBy(x => x.ConversationId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();
    public Task<bool> HasParticipantAsync(Guid conversationId, Guid userId) =>
        db.Messages.AnyAsync(x => x.ConversationId == conversationId && (x.Sender == userId || x.Receiver == userId));
    public Task<int> CountUnansweredMessagesAsync(Guid userId) =>
        db.Messages.AsNoTracking().CountAsync(message => message.Receiver == userId &&
            !db.Messages.Any(reply => reply.ConversationId == message.ConversationId &&
                reply.Sender == userId && reply.ChatAt >= message.ChatAt));
    public Task<List<T>> ListMessagesAsync<T>(Guid conversationId, Guid userId, int skip, int take,
        Expression<Func<Message, T>> selector) =>
        db.Messages.AsNoTracking().Where(x => x.ConversationId == conversationId && (x.Sender == userId || x.Receiver == userId))
            .OrderByDescending(x => x.ChatAt).ThenByDescending(x => x.MessageId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();
    public async Task<Conversation?> FindConversationAsync(Guid id) => await db.Conversations.FindAsync(id);
    public async Task<bool> ContainsOnlyParticipantsAsync(Guid conversationId, Guid sender, Guid receiver) =>
        await db.Messages.AnyAsync(x => x.ConversationId == conversationId) &&
        !await db.Messages.AnyAsync(x => x.ConversationId == conversationId &&
            ((x.Sender != sender && x.Sender != receiver) || (x.Receiver != sender && x.Receiver != receiver)));
    public void AddConversation(Conversation conversation) => db.Conversations.Add(conversation);
    public void AddMessage(Message message) => db.Messages.Add(message);
    public Task<int> SaveChangesAsync() => db.SaveChangesAsync();
}
