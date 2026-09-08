using Microsoft.AspNetCore.Mvc.Testing;

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
        var type = typeof(BloodLink.Infrastructure.Identity.ApplicationUser);
        Assert.NotNull(type.GetProperty("FacilityId"));
        Assert.NotNull(type.GetProperty("IsActive"));
    }

    [Fact]
    public void Application_StartsWithoutDependencyInjectionErrors()
    {
        // This will attempt to build the WebApplication host.
        // If there are any DI misconfigurations (e.g., missing services), 
        // CreateClient() will throw the AggregateException during startup.
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        Assert.NotNull(client);
    }
}
