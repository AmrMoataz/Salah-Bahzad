using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("staff");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FirebaseUid).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.FirebaseUid }).IsUnique();

        builder.Property(s => s.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.Email }).IsUnique();

        builder.Property(s => s.Role).IsRequired();

        // Soft-delete columns
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);
        builder.Property(s => s.DeletedAtUtc);
        builder.Property(s => s.DeletedById);

        // Composite index for tenant + common query patterns
        builder.HasIndex(s => new { s.TenantId, s.IsActive, s.IsDeleted });
    }
}
