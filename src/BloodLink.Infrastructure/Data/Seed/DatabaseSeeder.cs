using BloodLink.Application.Contracts;
using BloodLink.Domain.Entities;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Infrastructure.Data.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<BloodLinkDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureRolesAsync(roleManager, cancellationToken);
        await EnsureDemoFacilitiesAsync(dbContext, cancellationToken);
        await EnsureDemoUsersAsync(dbContext, userManager, cancellationToken);
        await EnsureInventoryRowsAsync(dbContext, cancellationToken);
    }

    public static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager, CancellationToken cancellationToken = default)
    {
        foreach (var roleName in new[]
                 {
                     RoleNames.SystemAdmin,
                     RoleNames.FacilityAdmin,
                     RoleNames.FacilityStaff
                 })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var role = new IdentityRole(roleName);
            await roleManager.CreateAsync(role);
        }
    }

    private static async Task EnsureDemoFacilitiesAsync(BloodLinkDbContext dbContext, CancellationToken cancellationToken)
    {
        var facilities = new[]
        {
            new Facility
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "City General Hospital",
                FacilityType = FacilityType.Hospital,
                RegistrationNumber = "FAC-HOSP-001",
                Region = "Greater Accra",
                City = "Accra",
                Address = "1 Medical Avenue",
                ContactEmail = "admin.citygeneral@demo.local",
                ContactPhone = "+233200000001",
                Status = FacilityStatus.Approved,
                CreatedByUserId = "system-demo",
                ApprovedByUserId = "system-demo",
                CreatedAtUtc = DateTime.UtcNow,
                ApprovedAtUtc = DateTime.UtcNow
            },
            new Facility
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Northern Blood Bank",
                FacilityType = FacilityType.BloodBank,
                RegistrationNumber = "FAC-BBANK-001",
                Region = "Northern",
                City = "Tamale",
                Address = "2 Transfusion Road",
                ContactEmail = "admin.northernblood@demo.local",
                ContactPhone = "+233200000002",
                Status = FacilityStatus.Approved,
                CreatedByUserId = "system-demo",
                ApprovedByUserId = "system-demo",
                CreatedAtUtc = DateTime.UtcNow,
                ApprovedAtUtc = DateTime.UtcNow
            }
        };

        foreach (var facility in facilities)
        {
            var exists = await dbContext.Facilities
                .AnyAsync(existing => existing.Id == facility.Id || existing.RegistrationNumber == facility.RegistrationNumber, cancellationToken);

            if (exists)
            {
                continue;
            }

            await dbContext.Facilities.AddAsync(facility, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoUsersAsync(BloodLinkDbContext dbContext, UserManager<ApplicationUser> userManager, CancellationToken cancellationToken)
    {
        var facilityAdmin = await dbContext.Facilities.FirstOrDefaultAsync(f => f.RegistrationNumber == "FAC-HOSP-001", cancellationToken);
        var bloodBankAdmin = await dbContext.Facilities.FirstOrDefaultAsync(f => f.RegistrationNumber == "FAC-BBANK-001", cancellationToken);

        if (facilityAdmin is null || bloodBankAdmin is null)
        {
            return;
        }

        var demoUsers = new[]
        {
            new ApplicationUser
            {
                Id = "demo-system-admin",
                UserName = "demo-system-admin",
                Email = "system.admin@demo.local",
                FirstName = "System",
                LastName = "Administrator",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = "demo-city-admin",
                UserName = "demo-city-admin",
                Email = "city.admin@demo.local",
                FirstName = "City",
                LastName = "Administrator",
                FacilityId = facilityAdmin.Id,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = "demo-city-staff",
                UserName = "demo-city-staff",
                Email = "city.staff@demo.local",
                FirstName = "City",
                LastName = "Staff",
                FacilityId = facilityAdmin.Id,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = "demo-bank-admin",
                UserName = "demo-bank-admin",
                Email = "bank.admin@demo.local",
                FirstName = "Northern",
                LastName = "Administrator",
                FacilityId = bloodBankAdmin.Id,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = "demo-bank-staff",
                UserName = "demo-bank-staff",
                Email = "bank.staff@demo.local",
                FirstName = "Northern",
                LastName = "Staff",
                FacilityId = bloodBankAdmin.Id,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        foreach (var user in demoUsers)
        {
            var existing = await userManager.FindByIdAsync(user.Id);
            if (existing is not null)
            {
                continue;
            }

            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create demo user {user.UserName}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }

        await EnsureUserRoleAsync(userManager, "demo-system-admin", RoleNames.SystemAdmin);
        await EnsureUserRoleAsync(userManager, "demo-city-admin", RoleNames.FacilityAdmin);
        await EnsureUserRoleAsync(userManager, "demo-city-staff", RoleNames.FacilityStaff);
        await EnsureUserRoleAsync(userManager, "demo-bank-admin", RoleNames.FacilityAdmin);
        await EnsureUserRoleAsync(userManager, "demo-bank-staff", RoleNames.FacilityStaff);

        var cityStaff = new FacilityStaff
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FacilityId = facilityAdmin.Id,
            UserId = "demo-city-staff",
            Status = StaffStatus.Active,
            CreatedByAdminId = "demo-city-admin",
            CreatedAtUtc = DateTime.UtcNow
        };

        var bankStaff = new FacilityStaff
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            FacilityId = bloodBankAdmin.Id,
            UserId = "demo-bank-staff",
            Status = StaffStatus.Active,
            CreatedByAdminId = "demo-bank-admin",
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!await dbContext.FacilityStaff.AnyAsync(staff => staff.UserId == cityStaff.UserId && staff.FacilityId == cityStaff.FacilityId, cancellationToken))
        {
            await dbContext.FacilityStaff.AddAsync(cityStaff, cancellationToken);
        }

        if (!await dbContext.FacilityStaff.AnyAsync(staff => staff.UserId == bankStaff.UserId && staff.FacilityId == bankStaff.FacilityId, cancellationToken))
        {
            await dbContext.FacilityStaff.AddAsync(bankStaff, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUserRoleAsync(UserManager<ApplicationUser> userManager, string userId, string roleName)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }

    private static async Task EnsureInventoryRowsAsync(BloodLinkDbContext dbContext, CancellationToken cancellationToken)
    {
        var facilities = await dbContext.Facilities
            .Where(facility => facility.Status == FacilityStatus.Approved)
            .Select(facility => facility.Id)
            .ToListAsync(cancellationToken);

        foreach (var facilityId in facilities)
        {
            foreach (var bloodType in Enum.GetValues<BloodType>())
            {
                var exists = await dbContext.BloodInventory.AnyAsync(item => item.FacilityId == facilityId && item.BloodType == bloodType, cancellationToken);
                if (exists)
                {
                    continue;
                }

                var inventory = new BloodInventory
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    BloodType = bloodType,
                    TotalUnits = 10,
                    ReservedUnits = 0,
                    LowStockThreshold = 3,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await dbContext.BloodInventory.AddAsync(inventory, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
