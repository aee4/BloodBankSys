using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BloodLink.Infrastructure.Tests;

public sealed class DatabaseModelConfigurationTests
{
    private static readonly BloodLinkDbContext Context = new(
        new DbContextOptionsBuilder<BloodLinkDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModelOnly;Trusted_Connection=True")
            .Options);
    private static readonly IModel Model = Context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Inventory_model_enforces_counts_uniqueness_and_concurrency()
    {
        var entity = Model.FindEntityType("BloodLink.Domain.Entities.BloodInventory")!;

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && PropertyNames(index).SequenceEqual(["FacilityId", "BloodType"]));
        Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Contains(entity.GetCheckConstraints(), constraint => constraint.Name == "CK_BloodInventory_ReservedWithinTotal");
        Assert.Contains(entity.GetCheckConstraints(), constraint => constraint.Name == "CK_BloodInventory_TotalUnits_NonNegative");
    }

    [Fact]
    public void Request_model_enforces_valid_units_and_operational_relationships()
    {
        var entity = Model.FindEntityType("BloodLink.Domain.Entities.BloodRequest")!;

        Assert.Contains(entity.GetCheckConstraints(), constraint => constraint.Name == "CK_BloodRequests_UnitsAcceptedWithinRequested");
        Assert.Contains(entity.GetCheckConstraints(), constraint => constraint.Name == "CK_BloodRequests_DistinctFacilities");
        Assert.All(entity.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
    }

    [Fact]
    public void Facility_and_staff_natural_keys_are_unique()
    {
        var facility = Model.FindEntityType("BloodLink.Domain.Entities.Facility")!;
        var staff = Model.FindEntityType("BloodLink.Domain.Entities.FacilityStaff")!;

        Assert.Contains(facility.GetIndexes(), index => index.IsUnique && PropertyNames(index).SequenceEqual(["Name"]));
        Assert.Contains(facility.GetIndexes(), index => index.IsUnique && PropertyNames(index).SequenceEqual(["RegistrationNumber"]));
        Assert.Contains(staff.GetIndexes(), index => index.IsUnique && PropertyNames(index).SequenceEqual(["UserId"]));
    }

    [Fact]
    public void User_facility_relationship_is_restrictive_and_names_are_bounded()
    {
        var user = Model.FindEntityType(typeof(ApplicationUser))!;
        var foreignKey = Assert.Single(user.GetForeignKeys(), key => PropertyNames(key).SequenceEqual(["FacilityId"]));

        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
        Assert.Equal(100, user.FindProperty(nameof(ApplicationUser.FirstName))!.GetMaxLength());
        Assert.Equal(100, user.FindProperty(nameof(ApplicationUser.LastName))!.GetMaxLength());
    }

    private static IEnumerable<string> PropertyNames(IReadOnlyIndex index) => index.Properties.Select(property => property.Name);
    private static IEnumerable<string> PropertyNames(IReadOnlyForeignKey foreignKey) => foreignKey.Properties.Select(property => property.Name);
}
