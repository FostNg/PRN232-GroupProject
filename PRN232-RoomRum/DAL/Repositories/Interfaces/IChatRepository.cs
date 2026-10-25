using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IChatRepository
{
    Task<List<T>> ListConversationsAsync<T>(Guid userId, int skip, int take, Expression<Func<Conversation, T>> selector);
    Task<bool> HasParticipantAsync(Guid conversationId, Guid userId);
    Task<List<T>> ListMessagesAsync<T>(Guid conversationId, Guid userId, int skip, int take, Expression<Func<Message, T>> selector);
    Task<Conversation?> FindConversationAsync(Guid id);
    Task<bool> ContainsOnlyParticipantsAsync(Guid conversationId, Guid sender, Guid receiver);
    void AddConversation(Conversation conversation);
    void AddMessage(Message message);
    Task<int> SaveChangesAsync();
}
