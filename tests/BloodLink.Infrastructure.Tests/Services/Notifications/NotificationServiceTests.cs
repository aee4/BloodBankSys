using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
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
