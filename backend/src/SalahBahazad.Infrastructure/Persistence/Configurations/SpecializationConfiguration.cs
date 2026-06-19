using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> builder)
    {
        builder.ToTable("specializations");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.SubjectId).IsRequired();

        // Soft-delete columns
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);
        builder.Property(s => s.DeletedAtUtc);
        builder.Property(s => s.DeletedById);

        // A specialization belongs to exactly one subject (FR-PLAT-TAX-002). Restrict delete:
        // a subject with live specializations cannot be removed (also enforced in the handler).
        builder.HasOne<Subject>()
            .WithMany()
            .HasForeignKey(s => s.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.SubjectId });

        // Unique name within a subject among live rows.
        builder.HasIndex(s => new { s.TenantId, s.SubjectId, s.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
