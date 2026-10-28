using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Implementations;
using RoomRum.DAL.Repositories.Interfaces;

namespace RoomRum.DAL;

public static class DataAccessRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RoomRumDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAmenityRepository, AmenityRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<ISharingRepository, SharingRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IRoomTenantRepository, RoomTenantRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IRepairRepository, RepairRepository>();
        return services;
    }
}
