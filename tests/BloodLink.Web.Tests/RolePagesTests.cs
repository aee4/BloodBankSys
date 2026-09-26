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
            "/requests/sent", "/requests/received", "/requests/3F2504E0-4F89-41D3-9A0C-0305E82C3301",
            "/facility/staff", "/facility/staff/create", "/facility/profile", "/notifications", "/system/facilities"
        })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/account/login", response.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Staff_RequestsSent_IsDeniedByRoutePolicy()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/requests/sent");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
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
    public async Task RequestDetail_ShowsCancellationOnlyToTheAuthenticatedSourceFacilityAdmin()
    {
        using var app = new SecurityTestApplication();
        var requester = await app.SeedAsync(RoleNames.FacilityAdmin);
        var source = await app.SeedAsync(RoleNames.FacilityAdmin);
        var unrelated = await app.SeedAsync(RoleNames.FacilityAdmin);
        var requestId = Guid.NewGuid();
        await using (var db = await DbAsync(app))
        {
            var requesterFacility = await db.Facilities.SingleAsync(item => item.Id == requester.FacilityId);
            requesterFacility.Name = "Requesting Hospital";
            var sourceFacility = await db.Facilities.SingleAsync(item => item.Id == source.FacilityId);
            sourceFacility.Name = "Source Blood Bank";
            var needId = Guid.NewGuid();
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = requesterFacility.Id,
                RequestedByUserId = requester.Id,
                BloodType = BloodType.APositive,
                UnitsNeeded = 5,
                Urgency = UrgencyLevel.Urgent,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Status = BloodNeedStatus.Searching,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.BloodRequests.Add(new BloodRequest
            {
                Id = requestId,
                BloodNeedId = needId,
                RequestingFacilityId = requesterFacility.Id,
                SourceFacilityId = sourceFacility.Id,
                BloodType = BloodType.APositive,
                UnitsRequested = 5,
                Status = BloodRequestStatus.Sent,
                RequestedByAdminId = requester.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var requesterClient = app.Browser();
        await SecurityTestApplication.LoginAsync(requesterClient, requester);
        var requesterResponse = await requesterClient.GetAsync($"/requests/{requestId}");
        var requesterHtml = await requesterResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, requesterResponse.StatusCode);
        Assert.Contains("Requesting Hospital", requesterHtml);
        Assert.Contains("Source Blood Bank", requesterHtml);
        Assert.DoesNotContain("Cancel request", requesterHtml);
        Assert.DoesNotContain("Source facility actions", requesterHtml);

        using var sourceClient = app.Browser();
        await SecurityTestApplication.LoginAsync(sourceClient, source);
        var sourceResponse = await sourceClient.GetAsync($"/requests/{requestId}");
        var sourceHtml = await sourceResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
        Assert.Contains("Cancel request", sourceHtml);
        Assert.Contains("Accept", sourceHtml);

        using var unrelatedClient = app.Browser();
        await SecurityTestApplication.LoginAsync(unrelatedClient, unrelated);
        var unrelatedResponse = await unrelatedClient.GetAsync($"/requests/{requestId}");
        var unrelatedHtml = await unrelatedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, unrelatedResponse.StatusCode);
        Assert.Contains("Request not found", unrelatedHtml);
        Assert.DoesNotContain("Requesting Hospital", unrelatedHtml);
        Assert.DoesNotContain("Source Blood Bank", unrelatedHtml);
    }

    [Fact]
    public async Task Admin_RequestsReceived_RendersIncomingRequest()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityAdmin);
        await using (var db = await DbAsync(app))
        {
            var requestingFacilityId = Guid.NewGuid();
            db.Facilities.Add(new Facility
            {
                Id = requestingFacilityId,
                Name = "Requesting Facility",
                RegistrationNumber = Guid.NewGuid().ToString("N"),
                Status = FacilityStatus.Approved
            });
            var needId = Guid.NewGuid();
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = requestingFacilityId,
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
                RequestingFacilityId = requestingFacilityId,
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

        var response = await client.GetAsync("/requests/received");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Requests Received", html);
        Assert.Contains("O-", html);
        Assert.Contains("Sent", html);
        Assert.Contains("Accept", html);
    }

    [Fact]
    public async Task Staff_SystemFacilities_IsDeniedByPolicy()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();

        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/system/facilities");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString);
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

        var response = await client.GetAsync("/facility/staff");

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

        var response = await client.GetAsync("/facility/profile");

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
    public async Task Notifications_LinkOnlyToAuthorizedAllowlistedRecords()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var user = await app.SeedAsync(RoleNames.FacilityStaff);
        var needId = Guid.NewGuid();
        await using (var db = await DbAsync(app))
        {
            db.BloodNeeds.Add(new BloodNeed
            {
                Id = needId,
                FacilityId = user.FacilityId!.Value,
                RequestedByUserId = user.Id,
                BloodType = BloodType.BPositive,
                UnitsNeeded = 2,
                Urgency = UrgencyLevel.Routine,
                NeededByUtc = DateTime.UtcNow.AddDays(1),
                Status = BloodNeedStatus.PendingReview,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            db.Notifications.AddRange(
                new Notification
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = user.Id,
                    NotificationType = NotificationType.NewNeed,
                    Title = "Need update",
                    Message = "Your need has an update.",
                    RelatedEntityType = nameof(BloodNeed),
                    RelatedEntityId = needId,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new Notification
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = user.Id,
                    NotificationType = NotificationType.Security,
                    Title = "Untrusted reference",
                    Message = "No external link is allowed.",
                    RelatedEntityType = "https://example.invalid/redirect",
                    RelatedEntityId = Guid.NewGuid(),
                    CreatedAtUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }
        await SecurityTestApplication.LoginAsync(client, user);

        var response = await client.GetAsync("/notifications");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"href=\"/needs/{needId}\"", html);
        Assert.DoesNotContain("https://example.invalid/redirect", html);
        Assert.Contains("Mark all read", html);
        Assert.Contains("Unread", html);
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
