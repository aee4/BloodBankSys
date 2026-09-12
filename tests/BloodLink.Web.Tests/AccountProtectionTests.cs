using System.Net;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using BloodLink.Web.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class AccountProtectionTests
{
    [Fact]
    public async Task RecoveryShutdown_CancelsInFlightDeliveryAndStopsAcceptingWork()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        app.Delivery.BlockUntil = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        using var client = app.Browser();
        await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!));
        await app.Delivery.WaitForAttemptsAsync(1);
        var queue = app.Services.GetRequiredService<PasswordRecoveryQueue>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await queue.StopAsync(timeout.Token);
        await app.Delivery.Canceled.Task.WaitAsync(timeout.Token);
        Assert.True(queue.ExecuteTask!.IsCompletedSuccessfully);
        Assert.False(queue.TryEnqueue(user.Email!));
        Assert.Empty(app.Delivery.Messages);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-password")]
    [InlineData("inactive")]
    [InlineData("pending")]
    [InlineData("rejected")]
    [InlineData("suspended")]
    [InlineData("no-role")]
    [InlineData("locked-out")]
    public async Task LoginFailure_IsGenericAndPerformsIdentityPasswordVerification(string scenario)
    {
        using var app = new SecurityTestApplication();
        var status = scenario switch
        {
            "pending" => FacilityStatus.Pending,
            "rejected" => FacilityStatus.Rejected,
            "suspended" => FacilityStatus.Suspended,
            _ => FacilityStatus.Approved
        };
        var user = await app.SeedAsync(active: scenario != "inactive", status: status);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var saved = (await users.FindByIdAsync(user.Id))!;
            if (scenario == "no-role") Assert.True((await users.RemoveFromRoleAsync(saved, "FacilityAdmin")).Succeeded);
            if (scenario == "locked-out") Assert.True((await users.SetLockoutEndDateAsync(saved, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
        }
        if (scenario == "unknown") user.Email = "unknown@example.test";
        using var client = app.Browser();
        var before = app.PasswordWork.Verifications;
        var result = await LoginAsync(client, user, "Incorrect123!");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal("/account/login?status=invalid", result.Headers.Location!.OriginalString);
        Assert.Equal(before + 1, app.PasswordWork.Verifications);
        Assert.DoesNotContain(result.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            cookie => cookie.StartsWith(".AspNetCore.Identity.Application="));
    }

    [Fact]
    public async Task IndependentQuotas_ThrottleLoginRecoveryAndPasswords_WithoutBlockingLogout()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var signedIn = app.Browser();
        using var anonymous = app.Browser();
        await LoginAsync(signedIn, user); // One of ten login permits used at this IP.
        for (var i = 0; i < 9; i++)
            await PostAsync(anonymous, "/account/login", "/account/login",
                ("Email", "missing@example.test"), ("Password", Password));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await anonymous.PostAsync("/account/login", new FormUrlEncodedContent([]))).StatusCode);

        for (var i = 0; i < 10; i++)
            await PostAsync(anonymous, "/account/forgot-password", "/account/forgot-password", ("Email", "missing@example.test"));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await anonymous.PostAsync("/account/forgot-password", new FormUrlEncodedContent([]))).StatusCode);

        for (var i = 0; i < 10; i++)
            await PostAsync(anonymous, "/account/reset-password?code=invalid", "/account/reset-password",
                ("Email", "missing@example.test"), ("Code", "invalid"), ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await anonymous.PostAsync("/account/reset-password", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal("/account/login?status=signed-out",
            (await PostAsync(signedIn, "/account/manage", "/account/logout")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RecoveryResponse_DoesNotWaitForProvider_OrDiscloseEligibility_AndWorkerSurvivesFailure()
    {
        using var app = new SecurityTestApplication();
        var eligible = await app.SeedAsync();
        var inactive = await app.SeedAsync(active: false);
        var blocked = await app.SeedAsync(status: FacilityStatus.Suspended);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Delivery.BlockUntil = gate.Task;
        using var client = app.Browser();
        client.DefaultRequestHeaders.Host = "attacker.example";
        var results = new List<(HttpStatusCode Status, string Location, string Body)>();
        try
        {
            // Delivery remains blocked until every public response has arrived.
            foreach (var email in new[] { eligible.Email!, "missing@example.test", inactive.Email!, blocked.Email! })
            {
                var result = await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", email))
                    .WaitAsync(TimeSpan.FromSeconds(10));
                results.Add((result.StatusCode, result.Headers.Location!.OriginalString, await result.Content.ReadAsStringAsync()));
            }
            Assert.Single(results.Distinct());
            Assert.Equal("/account/forgot-password?status=requested", results[0].Location);
            await app.Delivery.WaitForAttemptsAsync(1);
            Assert.Empty(app.Delivery.Messages);
            app.Delivery.Fail = true;
        }
        finally { gate.TrySetResult(); }

        // FIFO processing: reaching a second eligible item proves earlier ineligible items were skipped.
        await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", eligible.Email!));
        await app.Delivery.WaitForAttemptsAsync(2);
        app.Delivery.Fail = false;
        await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", eligible.Email!));
        await app.Delivery.WaitForMessagesAsync(1);
        Assert.All(app.Delivery.Messages, message =>
        {
            Assert.Equal(eligible.Email, message.Email);
            Assert.StartsWith("https://localhost/account/reset-password?", message.Url);
        });
    }

    [Fact]
    public async Task RecoveryQueue_IsBounded_AndFullQueueKeepsGenericPublicResponse()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Delivery.BlockUntil = gate.Task;
        using var client = app.Browser();
        try
        {
            await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!));
            await app.Delivery.WaitForAttemptsAsync(1);
            var queue = app.Services.GetRequiredService<PasswordRecoveryQueue>();
            for (var i = 0; i < 100; i++) Assert.True(queue.TryEnqueue($"missing-{i}@example.test"));
            Assert.False(queue.TryEnqueue("over-capacity@example.test"));
            Assert.Equal("/account/forgot-password?status=requested", (await PostAsync(client,
                "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!))).Headers.Location!.OriginalString);
        }
        finally { gate.TrySetResult(); }
    }

    [Fact]
    public async Task RecoveryBurst_CoalescesQueuedRecipientsWithoutDisclosingAccounts()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        var sentinel = await app.SeedAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Delivery.BlockUntil = gate.Task;
        using var client = app.Browser();
        try
        {
            await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!));
            await app.Delivery.WaitForAttemptsAsync(1);
            var queue = app.Services.GetRequiredService<PasswordRecoveryQueue>();
            for (var i = 0; i < 200; i++) Assert.True(queue.TryEnqueue(i % 2 == 0 ? user.Email! : user.Email!.ToUpperInvariant()));
            Assert.True(queue.TryEnqueue(sentinel.Email!));
            foreach (var email in new[] { user.Email!, "unknown@example.test" })
                Assert.Equal("/account/forgot-password?status=requested", (await PostAsync(client,
                    "/account/forgot-password", "/account/forgot-password", ("Email", email))).Headers.Location!.OriginalString);
        }
        finally { gate.TrySetResult(); }
        // One in flight + one queued for the repeated recipient, then a distinct FIFO marker.
        await app.Delivery.WaitForMessagesAsync(3);
        Assert.Equal(2, app.Delivery.Messages.Count(message => message.Email == user.Email));
        Assert.Single(app.Delivery.Messages, message => message.Email == sentinel.Email);
    }
}
