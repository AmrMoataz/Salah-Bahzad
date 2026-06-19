using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("grades");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();

        // Soft-delete columns
        builder.Property(g => g.IsDeleted).HasDefaultValue(false);
        builder.Property(g => g.DeletedAtUtc);
        builder.Property(g => g.DeletedById);

        // Unique name per tenant among live rows — a name freed by archival can be reused.
        builder.HasIndex(g => new { g.TenantId, g.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
