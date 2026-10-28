using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoomRum.BLL.Services.Implementations;
using RoomRum.BLL.Services.Interfaces;
using RoomRum.DAL;

namespace RoomRum.BLL.Services;

public static class BusinessServicesRegistration
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataAccess(configuration.GetConnectionString("RoomRumConnection")
            ?? throw new InvalidOperationException("Missing RoomRumConnection."));
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IRoomImageService, RoomImageService>();
        services.AddHttpClient("maps", client => client.Timeout = TimeSpan.FromSeconds(12));
        services.AddSingleton<ILocationSearchService, LocationSearchService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<ISharingService, SharingService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IRoomTenantService, RoomTenantService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IRepairService, RepairService>();
        return services;
    }
}
