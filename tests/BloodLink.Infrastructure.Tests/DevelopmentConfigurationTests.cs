using BloodLink.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BloodLink.Infrastructure.Data;

namespace BloodLink.Infrastructure.Tests;

public sealed class DevelopmentConfigurationTests
{
    [Fact]
    public void Infrastructure_requires_the_standard_connection_string()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBloodLinkInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IDbContextFactory<BloodLinkDbContext>>());

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
        Assert.DoesNotContain("Server=", exception.Message);
    }

    [Fact]
    public void Infrastructure_accepts_a_configured_connection_string()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=BloodLink_ConfigurationTests;Trusted_Connection=True"
            }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBloodLinkInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IDbContextFactory<BloodLinkDbContext>>());
    }
}
