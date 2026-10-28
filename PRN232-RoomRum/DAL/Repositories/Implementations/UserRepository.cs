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
    public Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<User, T>> selector) =>
        Db.Users.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.UserId)
            .Skip(skip).Take(take).Select(selector).ToListAsync();
}
