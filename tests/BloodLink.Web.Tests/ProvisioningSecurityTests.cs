using System.Net;
using System.Security.Claims;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.Staff;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class ProvisioningSecurityTests
{
    [Fact]
    public async Task RealStaffProvisioning_IntegratesWithRecoveryAndLogin_ButStillNeedsLockoutAndActivationHandoff()
    {
        using var app = new SecurityTestApplication();
        var admin = await app.SeedAsync();
        ApplicationUser staffUser;
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(RoleNames.FacilityStaff))).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
            var current = new CurrentUserService(new HttpContextAccessor(), new FixedAuthentication(await app.PrincipalAsync(admin.Id)),
                scope.ServiceProvider.GetRequiredService<AccountAccessService>());
            var service = new StaffService(
                db,
                current,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
                app.Delivery,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<IConfiguration>());
            var created = await service.CreateStaffAsync(new CreateStaffRequest("Test", "Staff", "provisioned@example.test", "1234567890"));
            staffUser = await db.Users.SingleAsync(u => u.Id == created.UserId);
            Assert.True(staffUser.MustChangePassword);
            Assert.False(staffUser.LockoutEnabled); // Documents the existing Backend 1 integration gap.
            Assert.Equal(StaffStatus.PendingActivation, created.Status);
            Assert.Equal(admin.FacilityId, staffUser.FacilityId);
        }

        // The production service sends a reset-link setup message through test-only delivery.
        using var client = app.Browser();
        await app.Delivery.WaitForMessagesAsync(1);
        var code = QueryHelpers.ParseQuery(new Uri(app.Delivery.Messages[0].Url).Query)["code"].ToString();
        Assert.Equal("/account/login?status=password-reset", (await PostAsync(client,
            "/account/reset-password?code=" + Uri.EscapeDataString(code), "/account/reset-password", ("Email", staffUser.Email!), ("Code", code),
            ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword))).Headers.Location!.OriginalString);
        Assert.Equal("/account/manage", (await LoginAsync(client, staffUser, NewPassword)).Headers.Location!.OriginalString);
        Assert.Contains("awaiting activation", await client.GetStringAsync("/account/manage"));
        Assert.Contains("/account/access-denied", (await client.GetAsync("/security-probe/staff")).Headers.Location!.OriginalString);
        Assert.Contains("/account/access-denied", (await client.GetAsync("/security-probe/admin")).Headers.Location!.OriginalString);

        using var verification = app.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        Assert.False((await database.Users.SingleAsync(u => u.Id == staffUser.Id)).MustChangePassword);
        Assert.Equal(StaffStatus.PendingActivation,
            (await database.FacilityStaff.SingleAsync(s => s.UserId == staffUser.Id)).Status);
        // Simulate the lifecycle owner's activation, without changing their production service.
        (await database.FacilityStaff.SingleAsync(s => s.UserId == staffUser.Id)).Status = StaffStatus.Active;
        await database.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/security-probe/staff")).StatusCode);
    }

    private sealed class FixedAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }
}
