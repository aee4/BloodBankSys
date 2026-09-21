using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloodLink.Infrastructure.Data;

public sealed class BloodLinkDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BloodLinkDbContext>
{
    public BloodLinkDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BloodLinkDatabase")
            ?? "Server=(localdb)\\mssqllocaldb;Database=BloodLink_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<BloodLinkDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BloodLinkDbContext(options);
    }
}
