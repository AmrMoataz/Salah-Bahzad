using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Code"/> rows. The batch relationship is configured on <see cref="CodeBatchConfiguration"/>;
/// here the scalar columns, the redemption join, and the register indexes are defined — including the
/// tenant-unique serial (contract §5, FR-ADM-COD-005).
/// </summary>
internal sealed class CodeConfiguration : IEntityTypeConfiguration<Code>
{
    public void Configure(EntityTypeBuilder<Code> builder)
    {
        builder.ToTable("codes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Serial).HasMaxLength(20).IsRequired();
        builder.Property(c => c.BatchId).IsRequired();
        builder.Property(c => c.SessionId).IsRequired();
        builder.Property(c => c.Value).HasPrecision(18, 2);
        builder.Property(c => c.Status).IsRequired();

        // Redemption join (nullable cross-aggregate references; no FK constraint).
        builder.Property(c => c.RedeemedByStudentId);
        builder.Property(c => c.RedeemedEnrollmentId);
        builder.Property(c => c.RedeemedAtUtc);

        // Soft-delete columns
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);
        builder.Property(c => c.DeletedAtUtc);
        builder.Property(c => c.DeletedById);

        // Session the code redeems for; restrict delete.
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serials are unique per tenant (FR-ADM-COD-005). Includes soft-deleted rows, so a deleted serial is
        // never reissued; the generator seeds from this set with IgnoreQueryFilters.
        builder.HasIndex(c => new { c.TenantId, c.Serial }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.SessionId });
        builder.HasIndex(c => new { c.TenantId, c.BatchId });
    }
}
