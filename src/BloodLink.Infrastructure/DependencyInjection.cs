using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.DashboardServices;
using BloodLink.Infrastructure.Services.FacilityServices;
using BloodLink.Infrastructure.Services.Inventory;
using BloodLink.Infrastructure.Services.NeedServices;
using BloodLink.Infrastructure.Services.NotificationServices;
using BloodLink.Infrastructure.Services.RequestServices;
using BloodLink.Infrastructure.Services.StaffServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBloodLinkInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BloodLinkDatabase")
            ?? "Server=(localdb)\\mssqllocaldb;Database=BloodLink_Development;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<BloodLinkDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<BloodLinkDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddAuthorization(ConfigureAuthorization);

        // Add HTTP context accessor for CurrentUserService
        services.AddHttpContextAccessor();

        // Register application services
        // Security: Current User
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Backend Developer 1: Facilities & Staff
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IStaffService, StaffService>();

        // Backend Developer 2: Inventory
        services.AddScoped<IInventoryService, InventoryService>();

        // Backend Developer 3: Needs, Requests, Notifications, Dashboards
        services.AddScoped<IBloodNeedService, BloodNeedService>();
        services.AddScoped<IBloodRequestService, BloodRequestService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    private static void ConfigureAuthorization(AuthorizationOptions options)
    {
        options.AddPolicy(AuthorizationPolicies.RequireSystemAdmin, policy =>
            policy.RequireRole(RoleNames.SystemAdmin));

        options.AddPolicy(AuthorizationPolicies.RequireFacilityAdmin, policy =>
            policy.RequireRole(RoleNames.FacilityAdmin));

        options.AddPolicy(AuthorizationPolicies.RequireFacilityStaff, policy =>
            policy.RequireRole(RoleNames.FacilityStaff));

        options.AddPolicy(AuthorizationPolicies.RequireApprovedFacilityUser, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(RoleNames.FacilityAdmin, RoleNames.FacilityStaff);
            // TODO: Security owner must verify IsActive, FacilityId, and approved facility status in a handler.
        });
    }
}
