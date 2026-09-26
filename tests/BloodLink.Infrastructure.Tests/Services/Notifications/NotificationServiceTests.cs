using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Application.Contracts;
using BloodLink.Infrastructure.Services.Notifications;

namespace BloodLink.Infrastructure.Tests.Services.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task ListMineAndUnreadCountAreScopedToCurrentUser()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        dbContext.Notifications.AddRange(
            NewNotification("user-a", false),
            NewNotification("user-a", true),
            NewNotification("user-b", false));
        await dbContext.SaveChangesAsync();
        var service = new NotificationService(dbContext, User("user-a"));

        var mine = await service.ListMineAsync();
        var unread = await service.GetUnreadCountAsync();

        Assert.Equal(2, mine.Count);
        Assert.Equal(1, unread.Count);
        Assert.All(mine, notification => Assert.NotEqual(Guid.Empty, notification.Id));
    }

    [Fact]
    public async Task MarkReadAsync_OnlyMarksOwnNotification()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        var mine = NewNotification("user-a", false);
        var other = NewNotification("user-b", false);
        dbContext.Notifications.AddRange(mine, other);
        await dbContext.SaveChangesAsync();
        var service = new NotificationService(dbContext, User("user-a"));

        await service.MarkReadAsync(mine.Id);

        Assert.True(dbContext.Notifications.Single(notification => notification.Id == mine.Id).IsRead);
        Assert.NotNull(dbContext.Notifications.Single(notification => notification.Id == mine.Id).ReadAtUtc);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MarkReadAsync(other.Id));
    }

    [Fact]
    public async Task MarkAllReadAsync_AffectsOnlyCurrentUser()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        dbContext.Notifications.AddRange(
            NewNotification("user-a", false),
            NewNotification("user-a", false),
            NewNotification("user-b", false));
        await dbContext.SaveChangesAsync();
        var service = new NotificationService(dbContext, User("user-a"));

        await service.MarkAllReadAsync();

        Assert.All(dbContext.Notifications.Where(notification => notification.RecipientUserId == "user-a"), notification =>
        {
            Assert.True(notification.IsRead);
            Assert.NotNull(notification.ReadAtUtc);
        });
        Assert.False(dbContext.Notifications.Single(notification => notification.RecipientUserId == "user-b").IsRead);
    }

    [Fact]
    public async Task ListMineAsync_ExposesOnlyAllowlistedReferencesForParticipatingFacility()
    {
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
        WorkflowTestSupport.AddUser(dbContext, "admin-c", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityCId);
        var need = WorkflowTestSupport.AddNeed(dbContext, WorkflowTestSupport.FacilityAId, "staff-a", BloodNeedStatus.Searching);
        var request = WorkflowTestSupport.AddRequest(dbContext, need.Id, WorkflowTestSupport.FacilityAId, WorkflowTestSupport.FacilityBId);
        var allowedRequestNotice = NewNotification("admin-a", false);
        allowedRequestNotice.RelatedEntityType = nameof(BloodRequest);
        allowedRequestNotice.RelatedEntityId = request.Id;
        var unsafeNotice = NewNotification("admin-a", false);
        unsafeNotice.RelatedEntityType = "https://outside.invalid";
        unsafeNotice.RelatedEntityId = Guid.NewGuid();
        dbContext.Notifications.AddRange(allowedRequestNotice, unsafeNotice);
        await dbContext.SaveChangesAsync();

        var admin = new FakeCurrentUserService { UserId = "admin-a", FacilityId = WorkflowTestSupport.FacilityAId };
        admin.RoleList.Add(RoleNames.FacilityAdmin);
        var allowed = await new NotificationService(dbContext, admin).ListMineAsync();
        Assert.Equal(nameof(BloodRequest), Assert.Single(allowed, item => item.RelatedEntityId == request.Id).RelatedEntityType);
        Assert.Null(Assert.Single(allowed, item => item.RelatedEntityType is null).RelatedEntityId);

        var unrelated = new FakeCurrentUserService { UserId = "admin-c", FacilityId = WorkflowTestSupport.FacilityCId };
        unrelated.RoleList.Add(RoleNames.FacilityAdmin);
        var unrelatedNotice = NewNotification("admin-c", false);
        unrelatedNotice.RelatedEntityType = nameof(BloodRequest);
        unrelatedNotice.RelatedEntityId = request.Id;
        dbContext.Notifications.Add(unrelatedNotice);
        await dbContext.SaveChangesAsync();
        var hidden = Assert.Single(await new NotificationService(dbContext, unrelated).ListMineAsync());
        Assert.Null(hidden.RelatedEntityType);
        Assert.Null(hidden.RelatedEntityId);
    }

    private static Notification NewNotification(string recipientUserId, bool isRead) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NotificationType = NotificationType.Security,
            Title = "Title",
            Message = "Message",
            IsRead = isRead,
            CreatedAtUtc = DateTime.UtcNow,
            ReadAtUtc = isRead ? DateTime.UtcNow : null
        };

    private static FakeCurrentUserService User(string userId) =>
        new() { UserId = userId, FacilityId = WorkflowTestSupport.FacilityAId };
}
