using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ActorType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ActorRole).HasMaxLength(50);
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.Portal).HasMaxLength(20);
        builder.Property(a => a.DeviceId).HasMaxLength(256);
        builder.Property(a => a.PrevHash).HasMaxLength(64);
        builder.Property(a => a.Hash).HasMaxLength(64);

        // Indexes for investigation queries (FR-PLAT-AUD-004)
        builder.HasIndex(a => new { a.TenantId, a.OccurredAtUtc });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => new { a.TenantId, a.Action });
    }
}
