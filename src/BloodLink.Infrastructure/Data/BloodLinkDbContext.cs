using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Data;

public sealed class BloodLinkDbContext(DbContextOptions<BloodLinkDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityStaff> FacilityStaff => Set<FacilityStaff>();
    public DbSet<BloodInventory> BloodInventory => Set<BloodInventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<BloodNeed> BloodNeeds => Set<BloodNeed>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<BloodRequestStatusHistory> BloodRequestStatusHistory => Set<BloodRequestStatusHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Facility>()
            .HasIndex(facility => new { facility.Name, facility.RegistrationNumber })
            .IsUnique();

        builder.Entity<BloodInventory>()
            .HasIndex(inventory => new { inventory.FacilityId, inventory.BloodType })
            .IsUnique();

        builder.Entity<BloodInventory>()
            .Property(inventory => inventory.RowVersion)
            .IsRowVersion();

        builder.Entity<BloodNeed>()
            .Property(need => need.RowVersion)
            .IsRowVersion();

        builder.Entity<BloodRequest>()
            .Property(request => request.RowVersion)
            .IsRowVersion();

        // TODO: Database Developer 1 owns complete relationship, length, enum, and delete behavior configuration.
    }
}
