using System.Net;
using BloodLink.Application.Contracts;

namespace BloodLink.Web.Tests;

public sealed class DashboardPageTests
{
    [Fact]
    public async Task Anonymous_Request_RedirectsToLogin()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(RoleNames.FacilityStaff, "My Open Needs")]
    [InlineData(RoleNames.FacilityAdmin, "Inventory at a Glance")]
    [InlineData(RoleNames.SystemAdmin, "System Dashboard")]
    public async Task Authenticated_Role_SeesTheirDashboard(string role, string expectedText)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(role);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedText, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dashboard", html, StringComparison.OrdinalIgnoreCase);
    }
}