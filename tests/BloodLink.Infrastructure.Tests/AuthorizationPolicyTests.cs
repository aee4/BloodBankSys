using System.Security.Claims;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BloodLink.Infrastructure.Tests;

public sealed class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireSystemAdmin)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.FacilityStaff, AuthorizationPolicies.RequireFacilityStaff)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireApprovedFacilityUser)]
    [InlineData(RoleNames.FacilityStaff, AuthorizationPolicies.RequireApprovedFacilityUser)]
    public async Task ActiveUserWithAssignedRole_IsAuthorized(string role, string policy)
    {
        using var fixture = new Fixture(role);
        Assert.True(await fixture.AuthorizeAsync(policy));
    }

    [Theory]
    [InlineData(AuthorizationPolicies.RequireSystemAdmin)]
    [InlineData(AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(AuthorizationPolicies.RequireFacilityStaff)]
    [InlineData(AuthorizationPolicies.RequireApprovedFacilityUser)]
    public async Task AnonymousUser_IsDenied(string policy)
    {
        using var fixture = new Fixture(RoleNames.FacilityAdmin);
        Assert.False(await fixture.AuthorizeAsync(policy, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireSystemAdmin)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.FacilityStaff, AuthorizationPolicies.RequireFacilityStaff)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireApprovedFacilityUser)]
    public async Task InactiveOrDeletedAccount_IsDenied(string role, string policy)
    {
        using var fixture = new Fixture(role);
        fixture.User.IsActive = false;
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(policy));

        fixture.Database.Users.Remove(fixture.User);
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(policy));
    }

    [Theory]
    [InlineData(FacilityStatus.Pending)]
    [InlineData(FacilityStatus.Rejected)]
    [InlineData(FacilityStatus.Suspended)]
    public async Task BlockedFacility_IsDeniedByEveryFacilityPolicy(FacilityStatus status)
    {
        foreach (var role in new[] { RoleNames.FacilityAdmin, RoleNames.FacilityStaff })
        {
            using var fixture = new Fixture(role);
            fixture.Facility.Status = status;
            await fixture.Database.SaveChangesAsync();
            Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireApprovedFacilityUser));
            Assert.False(await fixture.AuthorizeAsync(role == RoleNames.FacilityAdmin
                ? AuthorizationPolicies.RequireFacilityAdmin : AuthorizationPolicies.RequireFacilityStaff));
        }
    }

    [Fact]
    public async Task FacilitySuspensionAndRestoration_AreObservedInSameScope()
    {
        using var fixture = new Fixture(RoleNames.FacilityAdmin);
        Assert.True(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin));
        fixture.Facility.Status = FacilityStatus.Suspended;
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin));
        fixture.Facility.Status = FacilityStatus.Approved;
        await fixture.Database.SaveChangesAsync();
        Assert.True(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin));
    }

    [Fact]
    public async Task MissingOrUnlinkedFacility_IsDeniedDespiteFacilityClaim()
    {
        using var fixture = new Fixture(RoleNames.FacilityAdmin);
        fixture.User.FacilityId = Guid.NewGuid();
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin));
        fixture.User.FacilityId = null;
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireApprovedFacilityUser));
    }

    [Fact]
    public async Task ClaimedFacility_CannotOverrideActualBlockedFacility()
    {
        using var fixture = new Fixture(RoleNames.FacilityAdmin);
        var blockedFacility = new Facility { Id = Guid.NewGuid(), Status = FacilityStatus.Suspended };
        fixture.Database.Facilities.Add(blockedFacility);
        fixture.User.FacilityId = blockedFacility.Id;
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireApprovedFacilityUser));
    }

    [Theory]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireSystemAdmin)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.FacilityStaff, AuthorizationPolicies.RequireFacilityStaff)]
    public async Task RevokedRole_IsDeniedWithUnchangedPrincipal(string role, string policy)
    {
        using var fixture = new Fixture(role);
        Assert.True(await fixture.AuthorizeAsync(policy));
        fixture.Database.UserRoles.Remove(fixture.Database.UserRoles.Single());
        await fixture.Database.SaveChangesAsync();
        Assert.False(await fixture.AuthorizeAsync(policy));
    }

    [Theory]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.SystemAdmin, AuthorizationPolicies.RequireApprovedFacilityUser)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireSystemAdmin)]
    [InlineData(RoleNames.FacilityStaff, AuthorizationPolicies.RequireFacilityAdmin)]
    [InlineData(RoleNames.FacilityAdmin, AuthorizationPolicies.RequireFacilityStaff)]
    public async Task DifferentRole_DoesNotGrantAccess(string role, string policy)
    {
        using var fixture = new Fixture(role);
        Assert.False(await fixture.AuthorizeAsync(policy));
    }

    [Fact]
    public async Task AssignedRoleWithoutClaim_AndAuthenticatedPrincipalWithoutId_AreDenied()
    {
        using var fixture = new Fixture(RoleNames.FacilityAdmin);
        var noRole = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, fixture.User.Id) }, "test"));
        var noId = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, RoleNames.FacilityAdmin) }, "test"));
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin, noRole));
        Assert.False(await fixture.AuthorizeAsync(AuthorizationPolicies.RequireFacilityAdmin, noId));
    }

    [Fact]
    public void CookieRedirects_MatchExistingAccountPages()
    {
        using var fixture = new Fixture(RoleNames.SystemAdmin);
        var options = fixture.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.Equal("/account/login", options.LoginPath.Value);
        Assert.Equal("/account/access-denied", options.AccessDeniedPath.Value);
        Assert.NotNull(options.Events.OnValidatePrincipal);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;
        private readonly ClaimsPrincipal principal;

        public Fixture(string roleName)
        {
            var options = new DbContextOptionsBuilder<BloodLinkDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Database = new BloodLinkDbContext(options);
            Facility = new Facility { Id = Guid.NewGuid(), Status = FacilityStatus.Approved };
            User = new ApplicationUser
            {
                Id = "user",
                IsActive = true,
                SecurityStamp = "test-stamp",
                FacilityId = roleName == RoleNames.SystemAdmin ? null : Facility.Id
            };
            var role = new IdentityRole(roleName);
            Database.Facilities.Add(Facility);
            Database.Users.Add(User);
            if (roleName == RoleNames.FacilityStaff)
                Database.FacilityStaff.Add(new FacilityStaff { UserId = User.Id, FacilityId = Facility.Id, Status = StaffStatus.Active });
            Database.Roles.Add(role);
            Database.UserRoles.Add(new IdentityUserRole<string> { UserId = User.Id, RoleId = role.Id });
            Database.SaveChanges();
            principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, User.Id),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("AspNet.Identity.SecurityStamp", User.SecurityStamp),
                new Claim("FacilityId", Facility.Id.ToString())
            }, "test"));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBloodLinkInfrastructure(new ConfigurationBuilder().Build());
            services.AddSingleton<IDbContextFactory<BloodLinkDbContext>>(new TestFactory(options));
            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
        }

        public BloodLinkDbContext Database { get; }
        public ApplicationUser User { get; }
        public Facility Facility { get; }
        public IServiceProvider Services => scope.ServiceProvider;

        public async Task<bool> AuthorizeAsync(string policy, ClaimsPrincipal? user = null) =>
            (await Services.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(user ?? principal, null, policy)).Succeeded;

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
            Database.Dispose();
        }
    }

    private sealed class TestFactory(DbContextOptions<BloodLinkDbContext> options)
        : IDbContextFactory<BloodLinkDbContext>
    {
        public BloodLinkDbContext CreateDbContext() => new(options);
    }
}
