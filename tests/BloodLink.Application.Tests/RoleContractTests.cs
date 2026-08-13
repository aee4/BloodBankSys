using BloodLink.Application.Contracts;

namespace BloodLink.Application.Tests;

public class RoleContractTests
{
    [Fact]
    public void RoleNames_AreExplicitAndUnambiguous()
    {
        Assert.Equal("SystemAdmin", RoleNames.SystemAdmin);
        Assert.Equal("FacilityAdmin", RoleNames.FacilityAdmin);
        Assert.Equal("FacilityStaff", RoleNames.FacilityStaff);
    }
}
