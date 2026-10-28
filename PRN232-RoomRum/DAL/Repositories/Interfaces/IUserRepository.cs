using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<bool> ExistsAsync(Guid id);
    Task<bool> EmailExistsAsync(string email);
    Task<User?> GetByEmailWithRoleAsync(string email);
    Task<User?> GetWithRoleAsync(Guid id);
    Task<Role?> GetRoleAsync(string? name);
    Task<bool> CreateRegistrationAsync(User user, IReadOnlyList<UserRegistrationFile> files,
        Func<Task<bool>>? beforeCommit);
    Task<VerificationEmailSendResult> ResendVerificationEmailAsync(Guid userId, Guid roleId, byte[] tokenHash,
        DateTime expiresAt, DateTime sentAt, Func<Task<bool>> send);
    Task<bool> VerifyEmailAsync(Guid userId, byte[] tokenHash, DateTime now);
    Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<User, T>> selector);
}

public enum VerificationEmailSendResult { Sent, Cooldown, DeliveryFailed }
