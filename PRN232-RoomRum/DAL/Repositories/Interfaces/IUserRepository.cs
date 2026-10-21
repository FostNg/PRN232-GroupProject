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
    Task<List<T>> ListAsync<T>(int skip, int take, Expression<Func<User, T>> selector);
}
