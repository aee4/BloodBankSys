using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.ActorUserId)
            .HasMaxLength(450);

        builder.Property(log => log.Action)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(log => log.EntityType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(log => log.Summary)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(log => log.IpAddress)
            .HasMaxLength(45);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .HasPrincipalKey(user => user.Id)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
