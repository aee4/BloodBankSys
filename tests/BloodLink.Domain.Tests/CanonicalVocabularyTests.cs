using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Tests;

public class CanonicalVocabularyTests
{
    [Fact]
    public void BloodType_DefinesAllExactTypes()
    {
        var values = Enum.GetNames<BloodType>();

        Assert.Equal(
            ["APositive", "ANegative", "BPositive", "BNegative", "ABPositive", "ABNegative", "OPositive", "ONegative"],
            values);
    }

    [Fact]
    public void Facility_RepresentsHospitalsAndBloodBanks()
    {
        var facility = new Facility
        {
            FacilityType = FacilityType.BloodBank,
            Name = "Central Blood Bank"
        };

        Assert.Equal(FacilityType.BloodBank, facility.FacilityType);
        Assert.Equal("Central Blood Bank", facility.Name);
    }
}
