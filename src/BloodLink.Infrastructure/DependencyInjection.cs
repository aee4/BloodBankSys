using BloodLink.Application.Contracts;
using BloodLink.Application.Interfaces;
using BloodLink.Application.Security;
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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            .AddSignInManager<BloodLinkSignInManager>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/access-denied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnValidatePrincipal = BloodLinkCookieValidation.ValidateAsync;
        });

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));
        services.AddScoped<AccountAccessService>();
        services.AddSingleton<LoginFailureWork>();
        services.TryAddSingleton<IPasswordResetDelivery, DisabledPasswordResetDelivery>();
        services.AddAuthorization(ConfigureAuthorization);
        services.AddScoped<IAuthorizationHandler, OperationalUserHandler>();
        services.AddScoped<IAuthorizationHandler, AccountSessionHandler>();
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
        options.AddPolicy(AccountSessionRequirement.Policy, policy =>
            policy.RequireAuthenticatedUser().AddRequirements(new AccountSessionRequirement()));
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new OperationalUserRequirement(false,
                RoleNames.SystemAdmin, RoleNames.FacilityAdmin, RoleNames.FacilityStaff))
            .Build();

        options.AddPolicy(AuthorizationPolicies.RequireSystemAdmin, policy =>
            policy.RequireAuthenticatedUser()
                .AddRequirements(new OperationalUserRequirement(false, RoleNames.SystemAdmin)));

        options.AddPolicy(AuthorizationPolicies.RequireFacilityAdmin, policy =>
            policy.RequireAuthenticatedUser()
                .AddRequirements(new OperationalUserRequirement(true, RoleNames.FacilityAdmin)));

        options.AddPolicy(AuthorizationPolicies.RequireFacilityStaff, policy =>
            policy.RequireAuthenticatedUser()
                .AddRequirements(new OperationalUserRequirement(true, RoleNames.FacilityStaff)));

        options.AddPolicy(AuthorizationPolicies.RequireApprovedFacilityUser, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new OperationalUserRequirement(
                true, RoleNames.FacilityAdmin, RoleNames.FacilityStaff));
        });
    }
}
