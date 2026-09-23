using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace BloodLink.Web.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Application_AssemblyName_MatchesExpectedProject()
    {
        Assert.Equal("BloodLink.Web", typeof(Program).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationUser_HasRequiredFacilityScopingFields()
    {
        var type = typeof(ApplicationUser);
        Assert.NotNull(type.GetProperty("FacilityId"));
        Assert.NotNull(type.GetProperty("IsActive"));
    }

    [Fact]
    public void Application_StartsWithoutDependencyInjectionErrors()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Theory]
    [InlineData(typeof(IBloodNeedService))]
    [InlineData(typeof(IBloodRequestService))]
    [InlineData(typeof(ICurrentUserService))]
    [InlineData(typeof(IDashboardService))]
    [InlineData(typeof(IFacilityService))]
    [InlineData(typeof(IInventoryService))]
    [InlineData(typeof(INotificationService))]
    [InlineData(typeof(IStaffService))]
    public void CoreApplicationServices_CanBeResolvedFromServiceScope(Type serviceType)
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetService(serviceType);
        Assert.NotNull(service);
    }

    [Fact]
    public void DatabaseContextFactory_IsRegistered()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetService<IDbContextFactory<BloodLinkDbContext>>();
        Assert.NotNull(dbFactory);
    }

    [Fact]
    public void IdentityServices_AreRegistered()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetService<SignInManager<ApplicationUser>>();
        Assert.NotNull(userManager);
        Assert.NotNull(signInManager);
    }

    [Fact]
    public async Task Application_RootEndpoint_RespondsWithoutCrashing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.True((int)response.StatusCode < 500, $"Root endpoint failed with status code {response.StatusCode}");
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\mssqllocaldb;Database=BloodLink_SmokeTests;Trusted_Connection=True"
                })));
}
