using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;
using RoomRum.API.Middleware;
using RoomRum.API.NewSchema;
using RoomRum.DAL;
using RoomRum.BLL;
using RoomRum.BLL.Settings;
using RoomRum.API.Registration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddDbContext<NewRoomRumDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("RoomRumConnection")
    ?? throw new InvalidOperationException("Missing RoomRumConnection.")));
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddHttpClient("maps", client => client.Timeout = TimeSpan.FromSeconds(12));
builder.Services.AddSingleton<ILocationSearchService, LocationSearchService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IRegistrationEmailSender, RegistrationEmailSender>();
var emailVerification = builder.Configuration.GetSection("EmailVerification").Get<EmailVerificationSettings>()
    ?? new EmailVerificationSettings();
if (!builder.Environment.IsDevelopment() &&
    (!Uri.TryCreate(emailVerification.ApiBaseUrl, UriKind.Absolute, out var verificationUri) || verificationUri.Scheme != "https"))
    emailVerification.ApiBaseUrl = string.Empty;
builder.Services.AddSingleton(emailVerification);
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<ISharingService, SharingService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IRoomTenantService, RoomTenantService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRepairService, RepairService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "Missing JWT settings. Copy appsettings.example.json to appsettings.json in RoomRum.API, " +
        "then configure the database connection and a private, random Jwt:Key. See README.md.");
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32 ||
    jwt.Key.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        "Jwt:Key must be a private, random secret of at least 32 UTF-8 bytes. " +
        "Replace the example value in appsettings.json or set Jwt__Key.");
if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must both be configured.");
if (jwt.AccessTokenMinutes <= 0)
    throw new InvalidOperationException("Jwt:AccessTokenMinutes must be positive.");
if (builder.Environment.IsProduction() && jwt.Key.Contains("Development-Secret-Key", StringComparison.Ordinal))
    throw new InvalidOperationException("Set Jwt:Key for production.");
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Fail("Invalid user identity.");
                return;
            }
            var db = context.HttpContext.RequestServices.GetRequiredService<NewRoomRumDbContext>();
            var user = await db.Users.Include(x => x.Role).AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == userId);
            if (user is null || user.Status != "Active" ||
                user.Role.RoleName != context.Principal?.FindFirstValue(ClaimTypes.Role))
                context.Fail("Account is inactive or its role changed.");
        }
    };
});
builder.Services.AddAuthorization();
var authPermitLimit = builder.Configuration.GetValue<int>("RateLimiting:AuthPermitLimit");
if (authPermitLimit < 1)
    throw new InvalidOperationException("RateLimiting:AuthPermitLimit must be positive.");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var configuredOrigin in builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
{
    if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("http" or "https") ||
        uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
        uri.UserInfo.Length > 0)
        throw new InvalidOperationException($"Invalid CORS origin: {configuredOrigin}");
    allowedOrigins.Add(uri.GetLeftPart(UriPartial.Authority));
}
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && uri.AbsolutePath == "/" &&
        uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0 &&
        (allowedOrigins.Contains(uri.GetLeftPart(UriPartial.Authority)) ||
         builder.Environment.IsDevelopment() && (uri.Host is "localhost" or "127.0.0.1")))
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddOpenApiDocument(document =>
{
    document.Title = "RoomRum API - RoomRum_db";
    document.AddSecurity("Bearer", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    document.OperationProcessors.Add(new ErrorResponsesOperationProcessor());
});

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiErrorEnvelopeMiddleware>();
app.UseCors();
app.UseOpenApi();
app.UseSwaggerUi();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
