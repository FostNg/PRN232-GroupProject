using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class UserRepository(RoomRumDbContext db) : Repository<User>(db), IUserRepository
{
    public Task<bool> ExistsAsync(Guid id) => Db.Users.AnyAsync(x => x.UserId == id);
    public Task<bool> EmailExistsAsync(string email) => Db.Users.AnyAsync(x => x.Email == email);
    public Task<User?> GetByEmailWithRoleAsync(string email) =>
        Db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Email == email);
    public Task<User?> GetWithRoleAsync(Guid id) =>
        Db.Users.Include(x => x.Role).AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
    public Task<Role?> GetRoleAsync(string? name) => Db.Roles.SingleOrDefaultAsync(x => x.RoleName == name);
    public async Task<bool> CreateRegistrationAsync(User user, IReadOnlyList<UserRegistrationFile> files,
        Func<Task<bool>>? beforeCommit)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync();
        Db.Users.Add(user);
        Db.UserRegistrationFiles.AddRange(files);
        await Db.SaveChangesAsync();
        if (beforeCommit is not null && !await beforeCommit()) return false;
        await transaction.CommitAsync();
        return true;
    }

    public async Task<VerificationEmailSendResult> ResendVerificationEmailAsync(Guid userId, Guid roleId, byte[] tokenHash,
        DateTime expiresAt, DateTime sentAt, Func<Task<bool>> send)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync();
        var updated = await Db.Users.Where(x => x.UserId == userId && x.RoleId == roleId &&
                x.Status == "Inactive" && x.EmailVerifiedAt == null &&
                (x.EmailVerificationSentAt == null || x.EmailVerificationSentAt <= sentAt.AddMinutes(-1)))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.EmailVerificationTokenHash, tokenHash)
                .SetProperty(x => x.EmailVerificationExpiresAt, expiresAt)
                .SetProperty(x => x.EmailVerificationSentAt, sentAt));
        if (updated != 1) return VerificationEmailSendResult.Cooldown;
        if (!await send()) return VerificationEmailSendResult.DeliveryFailed;
        await transaction.CommitAsync();
        return VerificationEmailSendResult.Sent;
    }

    public async Task<bool> VerifyEmailAsync(Guid userId, byte[] tokenHash, DateTime now) =>
        await Db.Users.Where(x => x.UserId == userId && x.Status == "Inactive" && x.EmailVerifiedAt == null &&
                x.EmailVerificationTokenHash == tokenHash && x.EmailVerificationExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Active")
                .SetProperty(x => x.EmailVerifiedAt, now)
                .SetProperty(x => x.EmailVerificationTokenHash, (byte[]?)null)
                .SetProperty(x => x.EmailVerificationExpiresAt, (DateTime?)null)) == 1;
    public Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<User, T>> selector) =>
        Db.Users.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.UserId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();
}
