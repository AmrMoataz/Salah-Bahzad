using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

internal sealed class CodeBatchConfiguration : IEntityTypeConfiguration<CodeBatch>
{
    public void Configure(EntityTypeBuilder<CodeBatch> builder)
    {
        builder.ToTable("code_batches");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Label).HasMaxLength(100).IsRequired();
        builder.Property(b => b.SessionId).IsRequired();
        builder.Property(b => b.Value).HasPrecision(18, 2);
        builder.Property(b => b.Quantity).IsRequired();

        // The session these codes target; restrict delete (sessions soft-delete, so codes never orphan).
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // The codes minted by this batch, managed through the root (field-backed navigation).
        builder.HasMany(b => b.Codes)
            .WithOne()
            .HasForeignKey(c => c.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.Codes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(b => new { b.TenantId, b.SessionId });
    }
}
