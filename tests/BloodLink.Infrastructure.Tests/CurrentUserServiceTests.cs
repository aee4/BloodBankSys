using System.Security.Claims;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Tests;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void AnonymousCircuitUser_HasNoCurrentUserDetails()
    {
        using var database = CreateDatabase();
        var service = CreateService(database.Factory, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Null(service.UserId);
        Assert.False(service.IsAuthenticated);
        Assert.Empty(service.Roles);
        Assert.Null(service.FacilityId);
        Assert.False(service.IsActive);
    }

    [Fact]
    public void NormalRequest_FallsBackToHttpContextWhenCircuitStateIsUnavailable()
    {
        using var database = CreateDatabase();
        var requestPrincipal = CreatePrincipal("request-user");
        var service = CreateService(
            database.Factory,
            new ServerAuthenticationStateProvider(),
            requestPrincipal);

        Assert.Equal("request-user", service.UserId);
        Assert.True(service.IsAuthenticated);
    }

    [Fact]
    public void InteractiveExecution_PrefersCircuitUserOverStaleHttpContextUser()
    {
        using var database = CreateDatabase();
        var circuitPrincipal = CreatePrincipal(
            "circuit-user",
            new Claim(ClaimTypes.Role, "FacilityStaff"));
        var staleRequestPrincipal = CreatePrincipal(
            "stale-request-user",
            new Claim(ClaimTypes.Role, "SystemAdmin"));
        var service = CreateService(database.Factory, circuitPrincipal, staleRequestPrincipal);

        Assert.Equal("circuit-user", service.UserId);
        Assert.True(service.IsInRole("FacilityStaff"));
        Assert.False(service.IsInRole("SystemAdmin"));
    }

    [Fact]
    public void PendingCircuitAuthenticationState_FailsClosedInsteadOfUsingStaleHttpContext()
    {
        using var database = CreateDatabase();
        var staleRequestPrincipal = CreatePrincipal("stale-request-user");
        var service = CreateService(
            database.Factory,
            new PendingAuthenticationStateProvider(),
            staleRequestPrincipal);

        Assert.Null(service.UserId);
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedUser_ReadsUserIdRolesAndRoleMembershipFromPrincipal()
    {
        using var database = CreateDatabase();
        var principal = CreatePrincipal(
            "user-123",
            new Claim(ClaimTypes.Role, "FacilityAdmin"),
            new Claim(ClaimTypes.Role, "FacilityStaff"),
            new Claim(ClaimTypes.Role, "facilityadmin"));
        var service = CreateService(database.Factory, principal);

        Assert.Equal("user-123", service.UserId);
        Assert.True(service.IsAuthenticated);
        Assert.Equal(2, service.Roles.Count);
        Assert.Contains("FacilityAdmin", service.Roles);
        Assert.Contains("FacilityStaff", service.Roles);
        Assert.True(service.IsInRole("FacilityAdmin"));
        Assert.True(service.IsInRole("facilitystaff"));
        Assert.False(service.IsInRole("SystemAdmin"));
        Assert.False(service.IsInRole(string.Empty));
    }

    [Fact]
    public void FacilityLinkedActiveUser_ReadsAuthoritativeIdentityState()
    {
        using var database = CreateDatabase();
        var facilityId = Guid.NewGuid();
        var untrustedFacilityId = Guid.NewGuid();
        AddUser(database.Context, "active-user", facilityId, isActive: true);
        var service = CreateService(
            database.Factory,
            CreatePrincipal("active-user", new Claim("FacilityId", untrustedFacilityId.ToString())));

        Assert.Equal(facilityId, service.FacilityId);
        Assert.True(service.BelongsToFacility(facilityId));
        Assert.False(service.BelongsToFacility(untrustedFacilityId));
        Assert.True(service.IsActive);
    }

    [Fact]
    public void FacilityId_IsRefreshedFromIdentityRecord()
    {
        using var database = CreateDatabase();
        var facilityAId = Guid.NewGuid();
        var facilityBId = Guid.NewGuid();
        var user = AddUser(database.Context, "facility-user", facilityAId, isActive: true);
        var service = CreateService(database.Factory, CreatePrincipal("facility-user"));

        Assert.Equal(facilityAId, service.FacilityId);

        user.FacilityId = facilityBId;
        database.Context.SaveChanges();

        Assert.Equal(facilityBId, service.FacilityId);
        Assert.False(service.BelongsToFacility(facilityAId));
        Assert.True(service.BelongsToFacility(facilityBId));
    }

    [Fact]
    public void ActiveSystemAdminWithoutFacility_HasNoFacilityMembership()
    {
        using var database = CreateDatabase();
        AddUser(database.Context, "system-admin", facilityId: null, isActive: true);
        var service = CreateService(
            database.Factory,
            CreatePrincipal("system-admin", new Claim(ClaimTypes.Role, "SystemAdmin")));

        Assert.Null(service.FacilityId);
        Assert.True(service.IsActive);
        Assert.True(service.IsInRole("SystemAdmin"));
        Assert.False(service.BelongsToFacility(Guid.NewGuid()));
    }

    [Fact]
    public void InactiveUser_ReportsInactive()
    {
        using var database = CreateDatabase();
        AddUser(database.Context, "inactive-user", Guid.NewGuid(), isActive: false);
        var service = CreateService(database.Factory, CreatePrincipal("inactive-user"));

        Assert.False(service.IsActive);
    }

    [Fact]
    public void ActiveState_IsRefreshedUsingANewContextPerLookup()
    {
        using var database = CreateDatabase();
        var user = AddUser(database.Context, "active-user", Guid.NewGuid(), isActive: true);
        var service = CreateService(database.Factory, CreatePrincipal("active-user"));

        Assert.True(service.IsActive);

        user.IsActive = false;
        database.Context.SaveChanges();

        Assert.False(service.IsActive);
        Assert.True(database.Factory.ContextsCreated >= 2);
        Assert.Throws<ObjectDisposedException>(() => database.Factory.LastCreated!.Users.ToList());
    }

    [Fact]
    public void MissingIdentityUser_IsHandledSafely()
    {
        using var database = CreateDatabase();
        var service = CreateService(database.Factory, CreatePrincipal("deleted-user"));

        Assert.Equal("deleted-user", service.UserId);
        Assert.True(service.IsAuthenticated);
        Assert.Null(service.FacilityId);
        Assert.False(service.IsActive);
        Assert.False(service.BelongsToFacility(Guid.NewGuid()));
    }

    private static TestDatabase CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<BloodLinkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDatabase(options);
    }

    private static CurrentUserService CreateService(
        TrackingDbContextFactory dbContextFactory,
        ClaimsPrincipal circuitPrincipal,
        ClaimsPrincipal? requestPrincipal = null) =>
        CreateService(
            dbContextFactory,
            new TestAuthenticationStateProvider(circuitPrincipal),
            requestPrincipal);

    private static CurrentUserService CreateService(
        TrackingDbContextFactory dbContextFactory,
        AuthenticationStateProvider authenticationStateProvider,
        ClaimsPrincipal? requestPrincipal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = requestPrincipal is null
                ? null
                : new DefaultHttpContext { User = requestPrincipal }
        };

        return new CurrentUserService(accessor, authenticationStateProvider, dbContextFactory);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, params Claim[] additionalClaims)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) }
            .Concat(additionalClaims);

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"));
    }

    private static ApplicationUser AddUser(
        BloodLinkDbContext dbContext,
        string userId,
        Guid? facilityId,
        bool isActive)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            FacilityId = facilityId,
            IsActive = isActive
        };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();

        return user;
    }

    private sealed class TestDatabase : IDisposable
    {
        public TestDatabase(DbContextOptions<BloodLinkDbContext> options)
        {
            Context = new BloodLinkDbContext(options);
            Factory = new TrackingDbContextFactory(options);
        }

        public BloodLinkDbContext Context { get; }
        public TrackingDbContextFactory Factory { get; }

        public void Dispose() => Context.Dispose();
    }

    private sealed class TrackingDbContextFactory(DbContextOptions<BloodLinkDbContext> options)
        : IDbContextFactory<BloodLinkDbContext>
    {
        public int ContextsCreated { get; private set; }
        public BloodLinkDbContext? LastCreated { get; private set; }

        public BloodLinkDbContext CreateDbContext()
        {
            ContextsCreated++;
            LastCreated = new BloodLinkDbContext(options);
            return LastCreated;
        }
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class PendingAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly TaskCompletionSource<AuthenticationState> authenticationState = new();

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            authenticationState.Task;
    }
}
