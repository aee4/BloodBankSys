using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.Inventory;
using BloodLink.Infrastructure.Services.Dashboard;
using BloodLink.Infrastructure.Services.Facilities;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Notifications;
using BloodLink.Infrastructure.Services.Requests;
using BloodLink.Infrastructure.Services.Staff;
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

        services.AddDbContextFactory<BloodLinkDbContext>(options =>
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

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddAuthorization(ConfigureAuthorization);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IBloodNeedService, BloodNeedService>();
        services.AddScoped<IBloodRequestService, BloodRequestService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInventoryService, InventoryService>();

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
