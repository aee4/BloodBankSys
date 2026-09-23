using System.Net;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BloodLink.Application.Contracts;
using BloodLink.Application.Security;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BloodLink.Web.Tests;

public sealed class SecurityTestApplication : WebApplicationFactory<Program>
{
    public const string Password = "Temporary123!";
    public const string NewPassword = "Changed456!";
    private readonly DbContextOptions<BloodLinkDbContext> options = new DbContextOptionsBuilder<BloodLinkDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    public TestDelivery Delivery { get; } = new();
    public PasswordWorkProbe PasswordWork { get; } = new();
    public bool RejectUserUpdates { get; set; }
    public string EnvironmentName { get; set; } = "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Account:PublicOrigin"] = "https://localhost",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=BloodLink_WebTests;Trusted_Connection=True"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BloodLinkDbContext>>();
            services.RemoveAll<IDbContextFactory<BloodLinkDbContext>>();
            services.RemoveAll<BloodLinkDbContext>();
            services.AddSingleton(options);
            services.AddSingleton<IDbContextFactory<BloodLinkDbContext>>(new TestContextFactory(options));
            services.AddScoped(_ => new BloodLinkDbContext(options));
            services.AddSingleton<IPasswordResetDelivery>(Delivery);
            services.AddScoped<IPasswordHasher<ApplicationUser>>(provider => new ObservedPasswordHasher(
                provider.GetRequiredService<IOptions<PasswordHasherOptions>>(), PasswordWork));
            services.AddScoped<IUserValidator<ApplicationUser>>(_ => new UpdateFailureValidator(this));
            services.AddControllers().AddApplicationPart(typeof(SecurityProbeController).Assembly);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public async Task<ApplicationUser> SeedAsync(string role = RoleNames.FacilityAdmin, bool mustChange = false,
        bool active = true, FacilityStatus status = FacilityStatus.Approved, bool linked = true,
        StaffStatus staffStatus = StaffStatus.Active, bool staffRow = true)
    {
        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BloodLinkDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        Guid? facilityId = null;
        if (linked && role != RoleNames.SystemAdmin)
        {
            var facility = new Facility { Id = Guid.NewGuid(), Status = status };
            database.Facilities.Add(facility);
            await database.SaveChangesAsync();
            facilityId = facility.Id;
        }
        var user = new ApplicationUser
        {
            UserName = $"{Guid.NewGuid():N}@example.test",
            IsActive = active,
            FacilityId = facilityId,
            MustChangePassword = mustChange,
            EmailConfirmed = true
        };
        user.Email = user.UserName;
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        if (role == RoleNames.FacilityStaff && staffRow && facilityId is { } staffFacility)
        {
            database.FacilityStaff.Add(new FacilityStaff
            {
                UserId = user.Id,
                FacilityId = staffFacility,
                Status = staffStatus,
                CreatedByAdminId = "test-admin"
            });
            await database.SaveChangesAsync();
        }
        return user;
    }

    public async Task ChangeAsync(string id, Action<ApplicationUser> change)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(id))!;
        change(user);
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    public async Task<ClaimsPrincipal> PrincipalAsync(string id)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>()
            .CreateAsync((await users.FindByIdAsync(id))!);
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string page, string action,
        params (string Key, string Value)[] values)
    {
        var response = await client.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Missing antiforgery input in " + page);
        var fields = values.ToDictionary(v => v.Key, v => v.Value);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        var result = await client.PostAsync(action, new FormUrlEncodedContent(fields));
        Assert.True(result.StatusCode == HttpStatusCode.Redirect,
            $"POST {action} returned {result.StatusCode}: {await result.Content.ReadAsStringAsync()}");
        return result;
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, ApplicationUser user, string? password = null,
        string? returnUrl = null) => PostAsync(client, "/account/login", "/account/login",
            ("Email", user.Email!), ("Password", password ?? Password), ("ReturnUrl", returnUrl ?? ""));

    public sealed class TestDelivery : IPasswordResetDelivery
    {
        private readonly ConcurrentQueue<(string Email, string Url)> messages = new();
        private readonly SemaphoreSlim changed = new(0);
        private int attempts;
        public bool IsConfigured { get; set; } = true;
        public bool Fail { get; set; }
        public Task? BlockUntil { get; set; }
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<(string Email, string Url)> Messages => messages.ToArray();
        public int Attempts => Volatile.Read(ref attempts);
        public async Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref attempts);
            changed.Release();
            try
            {
                if (BlockUntil is { } gate) await gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Canceled.TrySetResult();
                throw;
            }
            if (Fail) throw new InvalidOperationException("Simulated delivery failure");
            messages.Enqueue((email, resetUrl));
            changed.Release();
        }

        public async Task WaitForMessagesAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Messages.Count < count) await changed.WaitAsync(timeout.Token);
        }

        public async Task WaitForAttemptsAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Attempts < count) await changed.WaitAsync(timeout.Token);
        }
    }

    public sealed class PasswordWorkProbe
    {
        private int verifications;
        public int Verifications => Volatile.Read(ref verifications);
        public void Record() => Interlocked.Increment(ref verifications);
    }

    private sealed class ObservedPasswordHasher(IOptions<PasswordHasherOptions> options, PasswordWorkProbe probe)
        : PasswordHasher<ApplicationUser>(options)
    {
        public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            probe.Record();
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private sealed class UpdateFailureValidator(SecurityTestApplication app) : IUserValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(app.RejectUserUpdates
                ? IdentityResult.Failed(new IdentityError { Code = "SimulatedUpdateFailure", Description = "Simulated update failure." })
                : IdentityResult.Success);
    }

    private sealed class TestContextFactory(DbContextOptions<BloodLinkDbContext> options)
        : IDbContextFactory<BloodLinkDbContext>
    {
        public BloodLinkDbContext CreateDbContext() => new(options);
    }
}

// Test-only HTTP endpoints exercise the same production policies without adding business pages.
[Route("security-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [HttpGet("operational"), Authorize]
    public IActionResult OperationalPage() => Ok();
    [HttpGet("system"), Authorize(Policy = AuthorizationPolicies.RequireSystemAdmin)]
    public IActionResult SystemPage() => Ok();
    [HttpGet("admin"), Authorize(Policy = AuthorizationPolicies.RequireFacilityAdmin)]
    public IActionResult AdminPage() => Ok();
    [HttpGet("staff"), Authorize(Policy = AuthorizationPolicies.RequireFacilityStaff)]
    public IActionResult StaffPage() => Ok();
    [HttpGet("facility"), Authorize(Policy = AuthorizationPolicies.RequireApprovedFacilityUser)]
    public IActionResult FacilityPage() => Ok();
}
