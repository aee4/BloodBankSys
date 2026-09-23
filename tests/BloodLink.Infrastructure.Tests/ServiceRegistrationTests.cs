using System.Security.Claims;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Identity;
using BloodLink.Infrastructure.Services.Dashboard;
using BloodLink.Infrastructure.Services.Facilities;
using BloodLink.Infrastructure.Services.Inventory;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Notifications;
using BloodLink.Infrastructure.Services.Requests;
using BloodLink.Infrastructure.Services.Staff;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Infrastructure.Tests;

public sealed class ServiceRegistrationTests
{
    private static readonly IReadOnlyDictionary<Type, Type> CanonicalServices =
        new Dictionary<Type, Type>
        {
            [typeof(ICurrentUserService)] = typeof(CurrentUserService),
            [typeof(IFacilityService)] = typeof(FacilityService),
            [typeof(IStaffService)] = typeof(StaffService),
            [typeof(IBloodNeedService)] = typeof(BloodNeedService),
            [typeof(IBloodRequestService)] = typeof(BloodRequestService),
            [typeof(INotificationService)] = typeof(NotificationService),
            [typeof(IDashboardService)] = typeof(DashboardService),
            [typeof(IInventoryService)] = typeof(InventoryService)
        };

    [Fact]
    public void Production_services_have_one_canonical_scoped_registration()
    {
        var services = CreateServices();

        foreach (var (serviceType, implementationType) in CanonicalServices)
        {
            var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);

            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(implementationType, descriptor.ImplementationType);
        }
    }

    [Fact]
    public void Production_service_interfaces_have_one_concrete_implementation()
    {
        var implementationTypes = typeof(DependencyInjection).Assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToList();

        foreach (var (serviceType, canonicalType) in CanonicalServices)
        {
            var implementations = implementationTypes
                .Where(type => serviceType.IsAssignableFrom(type.AsType()))
                .Select(type => type.AsType())
                .ToList();

            Assert.Equal(new[] { canonicalType }, implementations);
        }
    }

    [Fact]
    public void Canonical_services_resolve_from_a_scope()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        foreach (var (serviceType, implementationType) in CanonicalServices)
        {
            var service = scope.ServiceProvider.GetRequiredService(serviceType);

            Assert.IsType(implementationType, service);
        }
    }

    private static ServiceCollection CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=BloodLink_ServiceRegistrationTests;Trusted_Connection=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();
        services.AddBloodLinkInfrastructure(configuration);
        return services;
    }

    private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
