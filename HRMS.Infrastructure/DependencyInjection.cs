using HRMS.Application.Auditing;
using HRMS.Application.Attendance;
using HRMS.Application.Reporting;
using HRMS.Application.External;
using HRMS.Application.LeaveManagement;
using HRMS.Infrastructure.Persistence;
using HRMS.Infrastructure.Auditing;
using HRMS.Infrastructure.Attendance;
using HRMS.Application.Common;
using HRMS.Infrastructure.Reporting;
using HRMS.Infrastructure.External;
using HRMS.Infrastructure.Identity;
using HRMS.Application.Identity;
using HRMS.Application.Employees;
using HRMS.Application.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace HRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HRMS");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The 'HRMS' connection string is not configured. " +
                "Set ConnectionStrings:HRMS in application configuration.");
        }

        services.AddDbContext<HRMSDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<HRMS.Application.Identity.IRoleStore, RoleStore>();
        services.AddScoped<ModuleData>();
        services.AddScoped<IEmployeeData>(sp => sp.GetRequiredService<ModuleData>());
        services.AddScoped<IOrganizationData>(sp => sp.GetRequiredService<ModuleData>());
        services.AddScoped<IAttendanceData>(sp => sp.GetRequiredService<ModuleData>());
        services.AddScoped<ILeaveData>(sp => sp.GetRequiredService<ModuleData>());
        services.AddScoped<IIdentityData>(sp => sp.GetRequiredService<ModuleData>());
        services.AddScoped<IEmployeeAccountLinks>(sp => sp.GetRequiredService<ModuleData>());
        services.AddIdentity<HRMSUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<HRMSDbContext>()
            .AddDefaultTokenProviders();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (userId is not null && !await context.HttpContext.RequestServices
                    .GetRequiredService<IUserAccountService>().CanSignInAsync(userId, context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.RequestServices.GetRequiredService<SignInManager<HRMSUser>>().SignOutAsync();
                }
            };
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });
        services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddSingleton<HRMS.Application.Common.IBusinessClock, BusinessClock>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<IRecordAccess, RecordAccess>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddHttpClient<IPublicHolidaysService, PublicHolidaysService>((provider, client) =>
        {
            var apiBaseAddress = configuration["ExternalApis:PublicHolidays:BaseAddress"]
                ?? "https://date.nager.at/api/v3/";
            if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var baseAddress) ||
                baseAddress.Scheme is not ("https" or "http"))
            {
                throw new InvalidOperationException("The public holidays API BaseAddress must be an absolute HTTP(S) URL.");
            }

            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
