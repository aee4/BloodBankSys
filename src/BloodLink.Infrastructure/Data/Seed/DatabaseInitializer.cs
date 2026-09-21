using System.Net.Mail;
using BloodLink.Application.Contracts;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BloodLink.Infrastructure.Data.Seed;

public sealed class DatabaseInitializer(
    BloodLinkDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    private static readonly string[] RoleNamesToSeed =
    [
        RoleNames.SystemAdmin,
        RoleNames.FacilityAdmin,
        RoleNames.FacilityStaff
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync();
        await BootstrapSystemAdminAsync(cancellationToken);
    }

    public async Task EnsureRolesAsync()
    {
        foreach (var roleName in RoleNamesToSeed)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(roleName)), $"create role {roleName}");
        }
    }

    public async Task BootstrapSystemAdminAsync(CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("BloodLink:BootstrapAdmin");
        if (!section.GetValue<bool>("Enabled"))
        {
            return;
        }

        var email = RequireValue(section, "Email");
        var password = RequireValue(section, "Password");
        var firstName = RequireValue(section, "FirstName");
        var lastName = RequireValue(section, "LastName");
        ValidateEmail(email);
        RejectKnownPlaceholder(password);

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.FacilityId is not null || !await userManager.IsInRoleAsync(existing, RoleNames.SystemAdmin))
            {
                throw new InvalidOperationException("The bootstrap email belongs to an account that is not the configured facility-less SystemAdmin.");
            }

            logger.LogInformation("The configured SystemAdmin already exists; bootstrap made no changes.");
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            FacilityId = null,
            IsActive = true,
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            EnsureSucceeded(await userManager.CreateAsync(user, password), "create the bootstrap SystemAdmin");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, RoleNames.SystemAdmin), "assign the SystemAdmin role");
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            else if (await userManager.FindByIdAsync(user.Id) is not null)
            {
                await userManager.DeleteAsync(user);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        logger.LogInformation("A facility-less SystemAdmin account was bootstrapped for {Email}.", email);
    }

    private static string RequireValue(IConfigurationSection section, string key)
    {
        var value = section[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"BloodLink:BootstrapAdmin:{key} is required when bootstrap is enabled.")
            : value;
    }

    private static void ValidateEmail(string email)
    {
        try
        {
            if (!string.Equals(new MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("BloodLink:BootstrapAdmin:Email must be a valid email address.");
        }
    }

    private static void RejectKnownPlaceholder(string password)
    {
        string[] prohibited = ["password", "password123", "changeme", "admin123", "temporary123!"];
        if (prohibited.Contains(password, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The bootstrap password is a known placeholder and cannot be used.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
