using System.Net;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Web.Tests;

public sealed class OperationalPageTests
{
    private readonly Guid FacilityId = Guid.NewGuid();

    private async ValueTask<BloodLinkDbContext> DbAsync(SecurityTestApplication app) =>
        (await app.Services.GetRequiredService<IDbContextFactory<BloodLinkDbContext>>().CreateDbContextAsync());

    [Fact]
    public async Task Anonymous_OperationalPages_RedirectToLogin()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        foreach (var path in new[] { "/needs/mine", "/needs", "/needs/new", "/inventory", "/inventory/adjust", "/inventory/history", "/inventory/search" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/account/login", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Staff_MyNeeds_RendersSubmittedNeed()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await using (var db = await DbAsync(app))
        {
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = Guid.NewGuid(),
                FacilityId = user.FacilityId!.Value,
                RequestedByUserId = user.Id,
                BloodType = BloodType.ONegative,
                UnitsNeeded = 4,
                Urgency = UrgencyLevel.Emergency,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Note = "Trauma unit",
                Status = BloodNeedStatus.PendingReview,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("My Submitted Needs", html);
        Assert.Contains("O-", html);
        Assert.Contains("4", html);
        Assert.Contains("Emergency", html);
    }

    [Fact]
    public async Task Admin_AllNeeds_RendersFacilityNeeds()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("All Needs", html);
        Assert.Contains("Pending Review", html);
    }

    [Fact]
    public async Task Staff_AllNeeds_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only facility administrators", html);
    }

    [Fact]
    public async Task Admin_MyNeeds_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only facility staff", html);
    }

    [Fact]
    public async Task Admin_InventoryOverview_RendersStock()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await using (var db = await DbAsync(app))
        {
            db.BloodInventory.Add(new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = user.FacilityId!.Value,
                BloodType = BloodType.OPositive,
                TotalUnits = 30,
                ReservedUnits = 2,
                LowStockThreshold = 5,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("28", html);
        Assert.Contains("Healthy", html);
        Assert.Contains("Adjust Inventory", html);
    }

    [Fact]
    public async Task Staff_InventoryAdjust_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory/adjust");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only facility administrators", html);
    }

    [Fact]
    public async Task Admin_InventoryHistory_RendersEmptyState()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Inventory History", html);
        Assert.Contains("No transactions yet", html);
    }
}