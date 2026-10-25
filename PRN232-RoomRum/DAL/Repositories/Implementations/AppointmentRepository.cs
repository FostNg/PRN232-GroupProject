using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL.Repositories.Implementations;

public sealed class AppointmentRepository(RoomRumDbContext db) : Repository<Appointment>(db), IAppointmentRepository
{
    public Task<List<T>> ListForUserAsync<T>(Guid userId, int skip, int take, Expression<Func<Appointment, T>> selector) =>
        Db.Appointments.AsNoTracking().Where(x => x.UserId == userId || x.Room.UserId == userId)
            .OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.AppointmentTime)
            .ThenBy(x => x.AppointmentId).Skip(skip).Take(take).Select(selector).ToListAsync();
    public Task<Appointment?> GetWithRoomAsync(Guid id) =>
        Db.Appointments.Include(x => x.Room).SingleOrDefaultAsync(x => x.AppointmentId == id);
    public Task<int> ChangeStatusAsync(Guid id, string expectedStatus, string nextStatus, DateTime updatedAt) =>
        Db.Appointments.Where(x => x.AppointmentId == id && x.Status == expectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, nextStatus)
                .SetProperty(x => x.UpdatedAt, updatedAt));
}
