using System.Net;
using BloodLink.Application.Contracts;
using BloodLink.Web.Components.Facility;
using BloodLink.Web.Components.Inventory;
using BloodLink.Web.Components.Requests;
using BloodLink.Web.Components.Staff;
using BloodLink.Web.Components.SystemAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace BloodLink.Web.Tests;

public sealed class RouteContractTests
{
    private static readonly IReadOnlyDictionary<string, Type> CanonicalRoutes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["/facility/profile"] = typeof(FacilityProfile),
            ["/facility/staff"] = typeof(StaffList),
            ["/facility/staff/create"] = typeof(StaffCreate),
            ["/requests/received"] = typeof(RequestsReceived)
        };

    private static readonly string[] LegacyRoutes =
    [
        "/facility",
        "/staff",
        "/staff/new",
        "/requests/in"
    ];

    private static readonly IReadOnlyDictionary<string, Type> SystemAdminRoutes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["/system/facilities"] = typeof(SystemFacilities),
            ["/system/facilities/{Id:guid}"] = typeof(SystemFacilityDetail)
        };

    private static readonly IReadOnlyDictionary<string, string> InventoryRoutePolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/inventory"] = AuthorizationPolicies.RequireApprovedFacilityUser,
            ["/inventory/history"] = AuthorizationPolicies.RequireApprovedFacilityUser,
            ["/inventory/adjust"] = AuthorizationPolicies.RequireFacilityAdmin,
            ["/inventory/search"] = AuthorizationPolicies.RequireFacilityAdmin
        };

    [Fact]
    public void Canonical_routes_have_one_owner_and_the_facility_admin_policy()
    {
        var routedComponents = typeof(FacilityProfile).Assembly.DefinedTypes
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .Select(route => (route.Template, Component: type.AsType())))
            .ToList();

        foreach (var (route, expectedComponent) in CanonicalRoutes)
        {
            var registration = Assert.Single(routedComponents, item => item.Template == route);
            Assert.Equal(expectedComponent, registration.Component);

            var authorization = Assert.Single(expectedComponent
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
            Assert.Equal(AuthorizationPolicies.RequireFacilityAdmin, authorization.Policy);
        }

        Assert.DoesNotContain(routedComponents, item => LegacyRoutes.Contains(item.Template, StringComparer.Ordinal));
        Assert.DoesNotContain(routedComponents.GroupBy(item => item.Template, StringComparer.Ordinal),
            group => group.Count() > 1);
    }

    [Fact]
    public void System_admin_facility_routes_have_one_owner_and_the_system_policy()
    {
        var routedComponents = typeof(FacilityProfile).Assembly.DefinedTypes
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .Select(route => (route.Template, Component: type.AsType())))
            .ToList();

        foreach (var (route, expectedComponent) in SystemAdminRoutes)
        {
            var registration = Assert.Single(routedComponents, item => item.Template == route);
            Assert.Equal(expectedComponent, registration.Component);

            var authorization = Assert.Single(expectedComponent
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
            Assert.Equal(AuthorizationPolicies.RequireSystemAdmin, authorization.Policy);
        }
    }

    [Fact]
    public void Inventory_routes_have_one_owner_and_expected_operational_policies()
    {
        var routedComponents = typeof(Inventory).Assembly.DefinedTypes
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .Select(route => (route.Template, Component: type.AsType())))
            .ToList();

        foreach (var (route, expectedPolicy) in InventoryRoutePolicies)
        {
            var registration = Assert.Single(routedComponents, item => item.Template == route);
            var authorization = Assert.Single(registration.Component
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

            Assert.Equal(expectedPolicy, authorization.Policy);
        }
    }

    [Fact]
    public async Task Facility_admin_navigation_uses_only_canonical_routes()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/dashboard");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var route in new[] { "/facility/profile", "/facility/staff", "/requests/received" })
        {
            Assert.Contains($"href=\"{route}\"", html, StringComparison.Ordinal);
        }
        foreach (var route in LegacyRoutes)
        {
            Assert.DoesNotContain($"href=\"{route}\"", html, StringComparison.Ordinal);
        }

        var staffResponse = await client.GetAsync("/facility/staff");
        var staffHtml = await staffResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, staffResponse.StatusCode);
        Assert.Contains("href=\"/facility/staff/create\"", staffHtml, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CanonicalRoutePaths))]
    public async Task Facility_admin_can_access_each_canonical_route(string route)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CanonicalRoutePaths))]
    public async Task Facility_staff_is_forbidden_from_each_canonical_route(string route)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Facility_admin_is_forbidden_from_system_facility_routes()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/system/facilities");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData(false, BloodLink.Domain.Enums.FacilityStatus.Approved)]
    [InlineData(true, BloodLink.Domain.Enums.FacilityStatus.Suspended)]
    public async Task Invalid_operational_session_cannot_access_canonical_routes(
        bool active,
        BloodLink.Domain.Enums.FacilityStatus facilityStatus)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin, active: active, status: facilityStatus);
        var login = await SecurityTestApplication.LoginAsync(client, user);
        Assert.Equal("/account/login?status=invalid", login.Headers.Location?.OriginalString);

        foreach (var route in CanonicalRoutes.Keys)
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/account/login", response.Headers.Location?.OriginalString);
        }
    }

    [Theory]
    [MemberData(nameof(LegacyRoutePaths))]
    public async Task Legacy_routes_are_not_routable(string route)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static IEnumerable<object[]> CanonicalRoutePaths() =>
        CanonicalRoutes.Keys.Select(route => new object[] { route });

    public static IEnumerable<object[]> LegacyRoutePaths() =>
        LegacyRoutes.Select(route => new object[] { route });
}
