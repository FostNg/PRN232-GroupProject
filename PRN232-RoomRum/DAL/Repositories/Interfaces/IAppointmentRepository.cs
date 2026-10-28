using System.Linq.Expressions;
using RoomRum.DAL.Entities;

namespace RoomRum.DAL.Repositories.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Appointment, T>> selector);
    Task<Appointment?> GetWithRoomAsync(Guid id);
    Task<int> ChangeStatusAsync(Guid id, string expectedStatus, string nextStatus, DateTime updatedAt);
}
