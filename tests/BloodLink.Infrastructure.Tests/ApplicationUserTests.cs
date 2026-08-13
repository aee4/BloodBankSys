using BloodLink.Infrastructure.Identity;

namespace BloodLink.Infrastructure.Tests;

public class ApplicationUserTests
{
    [Fact]
    public void ApplicationUser_CanRepresentPlatformUserWithoutFacility()
    {
        var user = new ApplicationUser { FacilityId = null, IsActive = true };

        Assert.Null(user.FacilityId);
        Assert.True(user.IsActive);
    }
}
