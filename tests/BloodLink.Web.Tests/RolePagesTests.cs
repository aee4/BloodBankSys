using System.Net;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Web.Tests;

public sealed class RolePagesTests
{
    private async ValueTask<BloodLinkDbContext> DbAsync(SecurityTestApplication app) =>
        (await app.Services.GetRequiredService<IDbContextFactory<BloodLinkDbContext>>().CreateDbContextAsync());

    [Fact]
    public async Task Anonymous_NewOperationalPages_RedirectToLogin()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        foreach (var path in new[]
        {
            "/requests/sent", "/requests/in", "/requests/3F2504E0-4F89-41D3-9A0C-0305E82C3301",
            "/staff", "/staff/new", "/facility", "/notifications", "/system/facilities"
        })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/account/login", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Staff_RequestsSent_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/requests/sent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only facility administrators", html);
    }

    [Fact]
    public async Task Admin_RequestsSentEmpty_RendersEmptyState()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/requests/sent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Requests Sent", html);
        Assert.Contains("No requests sent yet", html);
    }

    [Fact]
    public async Task Admin_RequestsReceived_RendersIncomingRequest()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await using (var db = await DbAsync(app))
        {
            var needId = Guid.NewGuid();
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = Guid.NewGuid(),
                RequestedByUserId = user.Id,
                BloodType = BloodType.ONegative,
                UnitsNeeded = 4,
                Urgency = UrgencyLevel.Emergency,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Note = "Trauma unit",
                Status = BloodNeedStatus.Searching,
                CreatedAtUtc = DateTime.UtcNow
            });
            db.BloodRequests.Add(new BloodRequest
            {
                Id = Guid.NewGuid(),
                BloodNeedId = needId,
                RequestingFacilityId = Guid.NewGuid(),
                SourceFacilityId = user.FacilityId!.Value,
                BloodType = BloodType.ONegative,
                UnitsRequested = 4,
                Status = BloodRequestStatus.Sent,
                RequestedByAdminId = "test-admin",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/requests/in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Requests Received", html);
        Assert.Contains("O-", html);
        Assert.Contains("Sent", html);
        Assert.Contains("Accept", html);
    }

    [Fact]
    public async Task Staff_SystemFacilities_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/system/facilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only system administrators", html);
    }

    [Fact]
    public async Task System_SystemFacilities_RendersPendingFacility()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.SystemAdmin);
        await using (var db = await DbAsync(app))
        {
            db.Facilities.Add(new Facility
            {
                Id = Guid.NewGuid(),
                Name = "Riverside Medical Centre",
                FacilityType = FacilityType.Hospital,
                RegistrationNumber = "REG-9021",
                Region = "Central",
                City = "Metroville",
                Status = FacilityStatus.Pending
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/system/facilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Facility Approvals", html);
        Assert.Contains("Riverside Medical Centre", html);
    }

    [Fact]
    public async Task Admin_StaffListEmpty_RendersEmptyState()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("No staff members yet", html);
        Assert.Contains("Add Staff Member", html);
    }

    [Fact]
    public async Task Admin_FacilityProfile_RendersDetails()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await using (var db = await DbAsync(app))
        {
            var facility = await db.Facilities.SingleAsync(f => f.Id == user.FacilityId);
            facility.Name = "Lakeside Hospital";
            facility.FacilityType = FacilityType.Hospital;
            facility.RegistrationNumber = "REG-7710";
            facility.Region = "North";
            facility.City = "Harbourtown";
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/facility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Lakeside Hospital", html);
        Assert.Contains("REG-7710", html);
        Assert.Contains("North", html);
        Assert.Contains("Edit Details", html);
    }

    [Fact]
    public async Task Admin_NotificationsEmpty_RendersEmptyState()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Notifications", html);
        Assert.Contains("No notifications", html);
    }

    [Fact]
    public async Task Admin_InventorySearch_ShowsSearchForm()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Network Blood Search", html);
        Assert.Contains("Search Network", html);
    }
}