using System.Net;
using System.Security.Claims;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class StaffLifecycleSecurityTests
{
    [Theory]
    [InlineData("active", true, true)]
    [InlineData("pending", true, false)]
    [InlineData("inactive", false, false)]
    [InlineData("missing", false, false)]
    [InlineData("mismatched", false, false)]
    [InlineData("duplicate", false, false)]
    public async Task StaffLifecycle_ControlsPoliciesScopeAndHttpAccess(string scenario, bool canSetUp, bool canOperate)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(RoleNames.FacilityStaff, staffStatus: scenario switch
        {
            "pending" => StaffStatus.PendingActivation,
            "inactive" => StaffStatus.Inactive,
            _ => StaffStatus.Active
        }, staffRow: scenario != "missing");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        if (scenario == "mismatched")
            (await db.FacilityStaff.SingleAsync(s => s.UserId == user.Id)).FacilityId = Guid.NewGuid();
        if (scenario == "duplicate")
            db.FacilityStaff.Add(new FacilityStaff { UserId = user.Id, FacilityId = user.FacilityId!.Value, Status = StaffStatus.Active });
        await db.SaveChangesAsync();
        var principal = await app.PrincipalAsync(user.Id);
        var access = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var current = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal), access);
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.Equal(canSetUp, (await authorization.AuthorizeAsync(principal, null, AccountSessionRequirement.Policy)).Succeeded);
        foreach (var policy in new[] { AuthorizationPolicies.RequireFacilityStaff, AuthorizationPolicies.RequireApprovedFacilityUser })
            Assert.Equal(canOperate, (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded);
        Assert.Equal(canOperate, current.IsActive);
        Assert.Equal(canOperate, current.IsInRole(RoleNames.FacilityStaff));
        Assert.Equal(canOperate ? user.FacilityId : null, current.FacilityId);
        if (!canOperate) Assert.Empty(current.Roles);
        using var client = app.Browser();
        Assert.Equal(canSetUp ? "/account/manage" : "/account/login?status=invalid",
            (await LoginAsync(client, user)).Headers.Location!.OriginalString);
        foreach (var path in new[] { "/security-probe/staff", "/security-probe/operational" })
        {
            var probe = await client.GetAsync(path);
            Assert.Equal(canOperate ? HttpStatusCode.OK : HttpStatusCode.Redirect, probe.StatusCode);
        }
        if (scenario == "pending") Assert.Contains("awaiting activation", await client.GetStringAsync("/account/manage"));
    }

    [Theory]
    [InlineData(StaffStatus.PendingActivation)]
    [InlineData(StaffStatus.Inactive)]
    public async Task StaffStatusChange_RevokesExistingOperationalCookieAndRunningCircuit(StaffStatus status)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        using var client = app.Browser();
        await LoginAsync(client, user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/security-probe/staff")).StatusCode);
        var principal = await app.PrincipalAsync(user.Id);
        using var scope = app.Services.CreateScope();
        var current = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal),
            scope.ServiceProvider.GetRequiredService<AccountAccessService>());
        using var provider = new FastProvider(app.Services.GetRequiredService<ILoggerFactory>(), app.Services.GetRequiredService<IServiceScopeFactory>());
        var rejected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.AuthenticationStateChanged += async state =>
        {
            if ((await state).User.Identity?.IsAuthenticated != true) rejected.TrySetResult();
        };
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        (await db.FacilityStaff.SingleAsync(s => s.UserId == user.Id)).Status = status;
        await db.SaveChangesAsync();
        Assert.False(current.IsActive);
        Assert.Empty(current.Roles);
        Assert.Null(current.FacilityId);
        await rejected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/security-probe/staff")).StatusCode);
        Assert.Equal(status == StaffStatus.PendingActivation ? HttpStatusCode.OK : HttpStatusCode.Redirect,
            (await client.GetAsync("/account/manage")).StatusCode);
    }

    [Fact]
    public async Task PendingStaff_CanChangePassword_ButCannotActivateThemselves()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(RoleNames.FacilityStaff, mustChange: true, staffStatus: StaffStatus.PendingActivation);
        using var client = app.Browser();
        Assert.Equal("/account/change-password", (await LoginAsync(client, user)).Headers.Location!.OriginalString);
        Assert.Equal("/account/manage?status=password-changed", (await PostAsync(client,
            "/account/change-password", "/account/change-password", ("CurrentPassword", Password),
            ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword))).Headers.Location!.OriginalString);
        Assert.Contains("awaiting activation", await client.GetStringAsync("/account/manage"));
        Assert.Contains("/account/access-denied", (await client.GetAsync("/security-probe/staff")).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/_blazor")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        Assert.Equal(StaffStatus.PendingActivation, (await db.FacilityStaff.SingleAsync(s => s.UserId == user.Id)).Status);
        Assert.False((await db.Users.SingleAsync(u => u.Id == user.Id)).MustChangePassword);
    }

    [Theory]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireSystemAdmin)]
    public async Task AdminAuthority_DoesNotDependOnStaffLifecycle(string role, string policy)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(role);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        db.FacilityStaff.Add(new FacilityStaff { UserId = user.Id, FacilityId = Guid.NewGuid(), Status = StaffStatus.Inactive });
        await db.SaveChangesAsync();
        var principal = await app.PrincipalAsync(user.Id);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy)).Succeeded);
        using var client = app.Browser();
        Assert.Equal("/account/manage", (await LoginAsync(client, user)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task MixedAdminStaffRoles_RetainAdminAuthorityButRequireActiveMembershipForStaffAuthority()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole(RoleNames.FacilityStaff));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(user.Id))!, RoleNames.FacilityStaff)).Succeeded);
        var principal = await app.PrincipalAsync(user.Id);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.True((await auth.AuthorizeAsync(principal, null, AuthorizationPolicies.RequireFacilityAdmin)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(principal, null, AuthorizationPolicies.RequireFacilityStaff)).Succeeded);
        var current = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal), scope.ServiceProvider.GetRequiredService<AccountAccessService>());
        Assert.True(current.IsInRole(RoleNames.FacilityAdmin));
        Assert.False(current.IsInRole(RoleNames.FacilityStaff));
    }

    private sealed class FixedAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class FastProvider(ILoggerFactory logger, IServiceScopeFactory scopes) : IdentityRevalidatingAuthenticationStateProvider(logger, scopes)
    {
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMilliseconds(20);
    }
}
