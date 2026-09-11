using System.Net;
using System.Security.Claims;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Web.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class SessionSecurityTests
{
    [Theory]
    [InlineData("deactivated")]
    [InlineData("deleted")]
    [InlineData("role-revoked")]
    [InlineData("stamp-changed")]
    [InlineData("suspended")]
    public async Task ChangedIdentity_InvalidatesCookieCircuitAndServiceAccess(string change)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var client = app.Browser();
        await LoginAsync(client, user);
        var principal = await app.PrincipalAsync(user.Id);
        using var scope = app.Services.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var currentUser = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal), access);
        using var provider = new ProbeProvider(app.Services.GetRequiredService<ILoggerFactory>(),
            app.Services.GetRequiredService<IServiceScopeFactory>());
        Assert.True(await provider.ValidateAsync(principal));
        Assert.True(currentUser.IsActive);
        Assert.True(currentUser.IsInRole(RoleNames.FacilityAdmin));

        using (var update = app.Services.CreateScope())
        {
            var users = update.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var saved = (await users.FindByIdAsync(user.Id))!;
            switch (change)
            {
                case "deactivated": saved.IsActive = false; await users.UpdateAsync(saved); break;
                case "deleted": await users.DeleteAsync(saved); break;
                case "role-revoked": await users.RemoveFromRoleAsync(saved, RoleNames.FacilityAdmin); break;
                case "stamp-changed": await users.UpdateSecurityStampAsync(saved); break;
                case "suspended":
                    var db = update.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
                    (await db.Facilities.SingleAsync(f => f.Id == saved.FacilityId)).Status = FacilityStatus.Suspended;
                    await db.SaveChangesAsync();
                    break;
            }
        }
        Assert.False(currentUser.IsActive);
        Assert.Empty(currentUser.Roles);
        Assert.Null(currentUser.FacilityId);
        Assert.False(currentUser.BelongsToFacility(user.FacilityId!.Value));
        Assert.False(await provider.ValidateAsync(principal));
        var rejected = await client.GetAsync("/account/manage");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("/account/login", rejected.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task MustChangePassword_BlocksServiceCallsButAllowsAccountSession()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(mustChange: true);
        var principal = await app.PrincipalAsync(user.Id);
        using var scope = app.Services.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var currentUser = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal), access);
        Assert.True(await access.ValidateSessionAsync(principal));
        Assert.True(currentUser.IsAuthenticated);
        Assert.False(currentUser.IsActive);
        Assert.Empty(currentUser.Roles);
        Assert.Null(currentUser.FacilityId);
        var authorization = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        Assert.True((await authorization.AuthorizeAsync(principal, null, AccountSessionRequirement.Policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, null, AuthorizationPolicies.RequireFacilityAdmin)).Succeeded);
    }

    [Fact]
    public async Task PasswordChange_RefreshesCurrentCookieAndInvalidatesOtherSessionAndCircuit()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var first = app.Browser();
        using var second = app.Browser();
        await LoginAsync(first, user);
        await LoginAsync(second, user);
        var oldPrincipal = await app.PrincipalAsync(user.Id);
        var result = await PostAsync(first, "/account/change-password", "/account/change-password",
            ("CurrentPassword", Password), ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword));
        Assert.Equal("/account/manage?status=password-changed", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/account/manage")).StatusCode);
        Assert.Contains("/account/login", (await second.GetAsync("/account/manage")).Headers.Location!.OriginalString);
        using var provider = new ProbeProvider(app.Services.GetRequiredService<ILoggerFactory>(),
            app.Services.GetRequiredService<IServiceScopeFactory>());
        Assert.False(await provider.ValidateAsync(oldPrincipal));
        Assert.True(await provider.ValidateAsync(await app.PrincipalAsync(user.Id)));
    }

    [Fact]
    public async Task SupportedRevalidationLoop_SignsOutInvalidCircuit()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        var principal = await app.PrincipalAsync(user.Id);
        using var provider = new ProbeProvider(app.Services.GetRequiredService<ILoggerFactory>(),
            app.Services.GetRequiredService<IServiceScopeFactory>());
        var signedOut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.AuthenticationStateChanged += async state =>
        {
            if ((await state).User.Identity?.IsAuthenticated != true) signedOut.TrySetResult();
        };
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));
        await app.ChangeAsync(user.Id, u => u.IsActive = false);
        await signedOut.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task FacilityClaims_CannotGrantCrossFacilityMembership()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        var principal = await app.PrincipalAsync(user.Id);
        var otherId = Guid.NewGuid();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("FacilityId", otherId.ToString()));
        using var scope = app.Services.CreateScope();
        var current = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(principal),
            scope.ServiceProvider.GetRequiredService<AccountAccessService>());
        Assert.Equal(user.FacilityId, current.FacilityId);
        Assert.False(current.BelongsToFacility(otherId));
    }

    private sealed class FixedAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class ProbeProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        : IdentityRevalidatingAuthenticationStateProvider(loggerFactory, scopeFactory)
    {
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMilliseconds(20);
        public Task<bool> ValidateAsync(ClaimsPrincipal user) =>
            ValidateAuthenticationStateAsync(new AuthenticationState(user), CancellationToken.None);
    }
}
