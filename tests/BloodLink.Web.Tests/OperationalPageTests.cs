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
    public async Task Staff_NewNeed_RendersRequiredValidationAndPrivacyGuidance()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs/new");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Blood type", html);
        Assert.Contains("Units needed", html);
        Assert.Contains("Urgency", html);
        Assert.Contains("Needed by", html);
        Assert.Contains("Do not include patient names", html);
    }

    [Fact]
    public async Task Admin_NeedDetail_UsesAuthorizedSnapshotInventoryAndPersistedTimeline()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        var needId = Guid.NewGuid();
        await using (var db = await DbAsync(app))
        {
            var facility = await db.Facilities.SingleAsync(item => item.Id == user.FacilityId);
            facility.Name = "Harbour Hospital";
            var need = new BloodNeed
            {
                Id = needId,
                FacilityId = facility.Id,
                RequestedByUserId = user.Id,
                BloodType = BloodType.ONegative,
                UnitsNeeded = 6,
                Urgency = UrgencyLevel.Urgent,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Status = BloodNeedStatus.Searching,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                Note = "Non-identifying clinical context"
            };
            db.BloodNeeds.Add(need);
            db.BloodNeedStatusHistory.Add(new BloodNeedStatusHistory
            {
                Id = Guid.NewGuid(),
                BloodNeedId = needId,
                FromStatus = null,
                ToStatus = BloodNeedStatus.Searching,
                ChangedByUserId = user.Id,
                ChangedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                Note = "Approved for sourcing"
            });
            db.BloodInventory.Add(new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = facility.Id,
                BloodType = BloodType.ONegative,
                TotalUnits = 20,
                ReservedUnits = 5,
                LowStockThreshold = 20,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync($"/needs/{needId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Harbour Hospital", html);
        Assert.Contains("Non-identifying clinical context", html);
        Assert.Contains("Matching facility inventory", html);
        Assert.Contains("20", html);
        Assert.Contains("15", html);
        Assert.Contains("Approved for sourcing", html);
        Assert.Contains(user.Email!, html);
        Assert.Contains("Search network", html);
    }

    [Fact]
    public async Task UnrelatedAdmin_NeedDetail_RendersNotFoundWithoutRecordData()
    {
        using var app = new SecurityTestApplication();
        var owner = await app.SeedAsync(RoleNames.FacilityAdmin);
        var unrelated = await app.SeedAsync(RoleNames.FacilityAdmin);
        var needId = Guid.NewGuid();
        await using (var db = await DbAsync(app))
        {
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = owner.FacilityId!.Value,
                RequestedByUserId = owner.Id,
                BloodType = BloodType.ONegative,
                UnitsNeeded = 7,
                Urgency = UrgencyLevel.Emergency,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Note = "Private facility need context",
                Status = BloodNeedStatus.PendingReview,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var client = app.Browser();
        await SecurityTestApplication.LoginAsync(client, unrelated);

        var response = await client.GetAsync($"/needs/{needId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Need not found", html);
        Assert.DoesNotContain("Private facility need context", html);
        Assert.DoesNotContain("O-", html);
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
    public async Task Staff_AllNeeds_IsDeniedByRoutePolicy()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Admin_MyNeeds_IsDeniedByRoutePolicy()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/needs/mine");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
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
        foreach (var bloodType in Enum.GetValues<BloodType>())
        {
            Assert.Contains($"/inventory/adjust?bloodType={bloodType}", html);
        }
    }

    [Fact]
    public async Task Staff_InventoryAdjust_ShowsNoAccess()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory/adjust");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Staff_InventoryOverview_IsViewOnly()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("View-only inventory", html);
        Assert.DoesNotContain("Adjust Inventory", html);
        Assert.DoesNotContain("Network Search", html);
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

    [Fact]
    public async Task Admin_InventoryHistory_RendersImmutableDetails()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await using (var db = await DbAsync(app))
        {
            var inventory = new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = user.FacilityId!.Value,
                BloodType = BloodType.ANegative,
                TotalUnits = 12,
                ReservedUnits = 2,
                LowStockThreshold = 5,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.BloodInventory.Add(inventory);
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                BloodInventoryId = inventory.Id,
                TransactionType = InventoryTransactionType.StockIn,
                TotalUnitsChange = 12,
                ReservedUnitsChange = 0,
                TotalAfter = 12,
                ReservedAfter = 2,
                Reason = "Initial stock",
                PerformedByUserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/inventory/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Initial stock", html);
        Assert.True(
            html.Contains("0 -&gt; 12", StringComparison.Ordinal) || html.Contains("0 -> 12", StringComparison.Ordinal),
            "History should show the total-units before/after balance.");
        Assert.True(
            html.Contains("2 -&gt; 2", StringComparison.Ordinal) || html.Contains("2 -> 2", StringComparison.Ordinal),
            "History should show the reserved-units before/after balance.");
        Assert.DoesNotContain("Delete", html);
    }

    [Fact]
    public async Task Admin_InventorySearch_IsPhase5BSearchOnly()
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
        Assert.DoesNotContain("Request Blood", html);
        Assert.DoesNotContain("Send Request", html);
    }

    [Fact]
    public async Task Admin_NetworkSearchHandoff_UsesNeedValuesInsteadOfQueryQuantities()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        var needId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        await using (var db = await DbAsync(app))
        {
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = user.FacilityId!.Value,
                RequestedByUserId = user.Id,
                BloodType = BloodType.ONegative,
                UnitsNeeded = 5,
                Urgency = UrgencyLevel.Urgent,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Status = BloodNeedStatus.Searching,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.Facilities.Add(new Facility { Id = sourceId, Name = "Approved Source", Status = FacilityStatus.Approved });
            db.BloodInventory.Add(new BloodInventory
            {
                Id = Guid.NewGuid(),
                FacilityId = sourceId,
                BloodType = BloodType.ONegative,
                TotalUnits = 9,
                ReservedUnits = 1,
                LowStockThreshold = 1,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync($"/inventory/search?needId={needId}&units=999&bloodType=ABPositive&returnUrl=https%3A%2F%2Fevil.invalid");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("5 units", html);
        Assert.Contains("O-", html);
        Assert.Contains("Approved Source", html);
        Assert.Contains("8 available", html);
        Assert.DoesNotContain("999", html);
        Assert.DoesNotContain("evil.invalid", html);
        Assert.Contains("Select source", html);
    }
}
