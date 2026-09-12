using System.Net;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class WebHardeningTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task Hsts_IsPresentForProductionHttps(string environment, bool expected)
    {
        using var app = new SecurityTestApplication { EnvironmentName = environment };
        using var client = app.Browser();
        client.DefaultRequestHeaders.Host = "bloodlink.example.test";
        var response = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, response.Headers.Contains("Strict-Transport-Security"));
        if (expected) Assert.Contains("max-age=", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Theory]
    [InlineData("/", "strict-origin-when-cross-origin")]
    [InlineData("/facility/register", "strict-origin-when-cross-origin")]
    [InlineData("/account/login", "no-referrer")]
    [InlineData("/account/forgot-password", "no-referrer")]
    [InlineData("/account/reset-password?code=test-placeholder", "no-referrer")]
    [InlineData("/account/access-denied", "no-referrer")]
    [InlineData("/app.css", "strict-origin-when-cross-origin")]
    public async Task PublicPagesAndAssets_HaveCompatibleSecurityHeaders(string path, string referrer)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal(referrer, Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("camera=(), microphone=(), geolocation=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")));
    }

    [Fact]
    public async Task ExplicitIdentityLockout_ProtectsUserManagerProvisionedAccountsForFifteenMinutes()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var scope = app.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;
        Assert.True(options.AllowedForNewUsers);
        Assert.Equal(5, options.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.DefaultLockoutTimeSpan);
        using var client = app.Browser();
        var started = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++) await LoginAsync(client, user, "Incorrect123!");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var saved = (await users.FindByIdAsync(user.Id))!;
        Assert.True(saved.LockoutEnabled);
        Assert.True(await users.IsLockedOutAsync(saved));
        Assert.InRange(saved.LockoutEnd!.Value, started.AddMinutes(15), DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.Equal("/account/login?status=invalid", (await LoginAsync(client, user)).Headers.Location!.OriginalString);
    }
}
