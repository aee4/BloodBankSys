using System.Net;
using BloodLink.Application.Contracts;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using static BloodLink.Web.Tests.SecurityTestApplication;

namespace BloodLink.Web.Tests;

public sealed class AccountFlowTests
{
    [Theory]
    [InlineData("/account/manage")]
    [InlineData("/account/change-password")]
    [InlineData("/security-probe/admin")]
    [InlineData("/security-probe/staff")]
    [InlineData("/security-probe/system")]
    [InlineData("/security-probe/operational")]
    public async Task AnonymousProtectedRequest_RedirectsToLogin(string path)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var result = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("/account/login?ReturnUrl=", result.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(RoleNames.SystemAdmin, "system", "admin")]
    [InlineData(RoleNames.FacilityAdmin, "admin", "system")]
    [InlineData(RoleNames.FacilityStaff, "staff", "admin")]
    public async Task RealLogin_SetsCookieUpdatesTimestampAndEnforcesRoles(string role, string allowed, string denied)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(role);
        using var client = app.Browser();
        var login = await LoginAsync(client, user);
        Assert.Equal("/account/manage", login.Headers.Location!.OriginalString);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application="));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/account/manage")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/security-probe/" + allowed)).StatusCode);
        var forbidden = await client.GetAsync("/security-probe/" + denied);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("/account/access-denied", forbidden.Headers.Location!.OriginalString);
        using var scope = app.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id);
        Assert.NotNull(saved!.LastLoginAtUtc);
        Assert.Contains(await scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>().AuditLogs.ToListAsync(),
            a => a.Action == "AccountLogin" && a.ActorUserId == user.Id);
    }

    [Theory]
    [InlineData(false, true, FacilityStatus.Approved)]
    [InlineData(true, false, FacilityStatus.Approved)]
    [InlineData(true, true, FacilityStatus.Pending)]
    [InlineData(true, true, FacilityStatus.Rejected)]
    [InlineData(true, true, FacilityStatus.Suspended)]
    public async Task IneligibleAccount_CannotLogin(bool active, bool linked, FacilityStatus status)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(active: active, linked: linked, status: status);
        using var client = app.Browser();
        Assert.Equal("/account/login?status=invalid", (await LoginAsync(client, user)).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account/manage")).StatusCode);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("missing-user")]
    [InlineData("revoked-role")]
    [InlineData("unknown-role")]
    [InlineData("deleted-user")]
    public async Task InvalidLogin_HasGenericFailure(string scenario)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(scenario == "unknown-role" ? "UnrecognizedRole" : RoleNames.FacilityAdmin);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (scenario == "deleted-user") await users.DeleteAsync((await users.FindByIdAsync(user.Id))!);
            if (scenario == "revoked-role") await users.RemoveFromRoleAsync((await users.FindByIdAsync(user.Id))!, RoleNames.FacilityAdmin);
        }
        if (scenario == "missing-user") user.Email = "missing@example.test";
        using var client = app.Browser();
        Assert.Equal("/account/login?status=invalid", (await LoginAsync(client, user,
            scenario == "wrong-password" ? "Incorrect123!" : Password)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RepeatedIncorrectPasswords_TriggerIdentityLockout()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var client = app.Browser();
        for (var i = 0; i < 5; i++) await LoginAsync(client, user, "Incorrect123!");
        Assert.Equal("/account/login?status=invalid", (await LoginAsync(client, user)).Headers.Location!.OriginalString);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id))!));
    }

    [Theory]
    [InlineData("https://evil.example/", "/account/manage")]
    [InlineData("//evil.example/", "/account/manage")]
    [InlineData("/\\evil.example/", "/account/manage")]
    [InlineData("javascript:alert(1)", "/account/manage")]
    [InlineData("/account/manage\r\nX-Injected: value", "/account/manage")]
    [InlineData("/security-probe/admin?tab=mine", "/security-probe/admin?tab=mine")]
    public async Task Login_OnlyUsesSafeLocalReturnUrls(string returnUrl, string expected)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var client = app.Browser();
        Assert.Equal(expected, (await LoginAsync(client, user, returnUrl: returnUrl)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_RequiresPostAndAntiforgery_AndClearsSession()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var client = app.Browser();
        await LoginAsync(client, user);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/account/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/account/logout", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/account/manage")).StatusCode);
        var result = await PostAsync(client, "/account/manage", "/account/logout");
        Assert.Equal("/account/login?status=signed-out", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account/manage")).StatusCode);
    }

    [Theory]
    [InlineData("/account/login")]
    [InlineData("/account/forgot-password")]
    [InlineData("/account/reset-password")]
    public async Task AnonymousAccountPosts_RejectMissingAntiforgery(string action)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(action, new FormUrlEncodedContent([]))).StatusCode);
    }

    [Fact]
    public async Task MustChangePassword_RestrictsNavigationAndRequiresCurrentPassword()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(mustChange: true);
        using var client = app.Browser();
        var result = await LoginAsync(client, user, returnUrl: "/security-probe/admin");
        Assert.Equal("/account/change-password", result.Headers.Location!.OriginalString);
        foreach (var path in new[] { "/", "/security-probe/admin", "/account/manage", "/facility/register" })
            Assert.Equal("/account/change-password", (await client.GetAsync(path)).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/security-probe/admin", new StringContent(""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/_blazor")).StatusCode);
        var wrong = await PostAsync(client, "/account/change-password", "/account/change-password",
            ("CurrentPassword", "Wrong123!"), ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword));
        Assert.Equal("/account/change-password?status=invalid", wrong.Headers.Location!.OriginalString);
        var correct = await PostAsync(client, "/account/change-password", "/account/change-password",
            ("CurrentPassword", Password), ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword));
        Assert.Equal("/account/manage?status=password-changed", correct.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/security-probe/admin")).StatusCode);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var saved = (await users.FindByIdAsync(user.Id))!;
        Assert.False(saved.MustChangePassword);
        Assert.NotEqual(user.SecurityStamp, saved.SecurityStamp);
        Assert.True(await users.CheckPasswordAsync(saved, NewPassword));
    }

    [Theory]
    [InlineData("weak", "weak")]
    [InlineData(NewPassword, "Mismatch789!")]
    [InlineData(Password, Password)]
    public async Task InvalidNewPassword_DoesNotClearRestriction(string password, string confirmation)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(mustChange: true);
        using var client = app.Browser();
        await LoginAsync(client, user);
        var result = await PostAsync(client, "/account/change-password", "/account/change-password",
            ("CurrentPassword", Password), ("NewPassword", password), ("ConfirmPassword", confirmation));
        Assert.Equal("/account/change-password?status=invalid", result.Headers.Location!.OriginalString);
        using var scope = app.Services.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id))!.MustChangePassword);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotEnumerateAccounts_OrTrustHostHeader()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        var inactive = await app.SeedAsync(active: false);
        using var client = app.Browser();
        client.DefaultRequestHeaders.Host = "attacker.example";
        foreach (var email in new[] { user.Email!, "unknown@example.test", inactive.Email! })
        {
            var result = await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", email));
            Assert.Equal("/account/forgot-password?status=requested", result.Headers.Location!.OriginalString);
        }
        await app.Delivery.WaitForMessagesAsync(1);
        Assert.Single(app.Delivery.Messages);
        Assert.StartsWith("https://localhost/account/reset-password?", app.Delivery.Messages[0].Url);
        app.Delivery.Fail = true;
        Assert.Equal("/account/forgot-password?status=requested", (await PostAsync(client,
            "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!))).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ResetToken_IsSingleUseAndInvalidatesExistingCookie()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var oldSession = app.Browser();
        await LoginAsync(oldSession, user);
        using var resetClient = app.Browser();
        var code = await RequestCodeAsync(app, resetClient, user);
        var result = await ResetAsync(resetClient, user.Email!, code);
        Assert.Equal("/account/login?status=password-reset", result.Headers.Location!.OriginalString);
        Assert.Contains("/account/login", (await oldSession.GetAsync("/account/manage")).Headers.Location!.OriginalString);
        Assert.Equal("/account/reset-password?status=invalid", (await ResetAsync(resetClient, user.Email!, code)).Headers.Location!.OriginalString);
        Assert.Equal("/account/manage", (await LoginAsync(resetClient, user, NewPassword)).Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("tampered")]
    [InlineData("other-account")]
    [InlineData("expired")]
    public async Task InvalidResetToken_IsRejected(string scenario)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        using var client = app.Browser();
        var code = await RequestCodeAsync(app, client, user);
        if (scenario == "malformed") code = "***invalid***";
        if (scenario == "tampered") code = "A" + code[1..];
        if (scenario == "other-account") user = await app.SeedAsync();
        if (scenario == "expired")
            app.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan = TimeSpan.FromSeconds(-1);
        Assert.Equal("/account/reset-password?status=invalid", (await ResetAsync(client, user.Email!, code)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ResetDoesNotReactivateAccount_AndClearsMustChangeOnlyOnSuccess()
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync(mustChange: true);
        using var client = app.Browser();
        var code = await RequestCodeAsync(app, client, user);
        await app.ChangeAsync(user.Id, u => u.IsActive = false);
        Assert.Equal("/account/login?status=password-reset", (await ResetAsync(client, user.Email!, code)).Headers.Location!.OriginalString);
        Assert.Equal("/account/login?status=invalid", (await LoginAsync(client, user, NewPassword)).Headers.Location!.OriginalString);
        using var scope = app.Services.CreateScope();
        var saved = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id))!;
        Assert.False(saved.IsActive);
        Assert.False(saved.MustChangePassword);
    }

    [Theory]
    [InlineData("/account/login")]
    [InlineData("/account/forgot-password")]
    [InlineData("/account/reset-password")]
    [InlineData("/account/access-denied")]
    public async Task AccountPages_ArePublicAndDoNotCacheOrLeakReferrers(string path)
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        var result = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", Assert.Single(result.Headers.GetValues("Referrer-Policy")));
    }

    private static async Task<string> RequestCodeAsync(SecurityTestApplication app, HttpClient client, ApplicationUser user)
    {
        var expected = app.Delivery.Messages.Count + 1;
        await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!));
        await app.Delivery.WaitForMessagesAsync(expected);
        return QueryHelpers.ParseQuery(new Uri(app.Delivery.Messages.Last().Url).Query)["code"].ToString();
    }

    [Fact]
    public async Task DisabledDelivery_UsesGenericResponseWithoutCreatingMessages()
    {
        using var app = new SecurityTestApplication();
        app.Delivery.IsConfigured = false;
        var user = await app.SeedAsync();
        using var client = app.Browser();
        Assert.Equal("/account/forgot-password?status=requested", (await PostAsync(client,
            "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!))).Headers.Location!.OriginalString);
        Assert.Empty(app.Delivery.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://localhost")]
    [InlineData("https://localhost/untrusted-path")]
    [InlineData("https://user:password@localhost")]
    public async Task InvalidPublicOrigin_DoesNotGenerateResetLink(string origin)
    {
        using var app = new SecurityTestApplication();
        var user = await app.SeedAsync();
        app.Services.GetRequiredService<IConfiguration>()["Account:PublicOrigin"] = origin;
        using var client = app.Browser();
        await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", user.Email!));
        Assert.Empty(app.Delivery.Messages);
    }

    [Fact]
    public async Task AccountPosts_AreRateLimited()
    {
        using var app = new SecurityTestApplication();
        using var client = app.Browser();
        for (var i = 0; i < 10; i++)
            await PostAsync(client, "/account/forgot-password", "/account/forgot-password", ("Email", "missing@example.test"));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent([]))).StatusCode);
    }

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string email, string code) =>
        PostAsync(client, "/account/reset-password?code=" + Uri.EscapeDataString(code), "/account/reset-password",
            ("Email", email), ("Code", code), ("NewPassword", NewPassword), ("ConfirmPassword", NewPassword));
}
