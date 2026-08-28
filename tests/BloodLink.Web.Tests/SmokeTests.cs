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
}
