using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SessionMaterial"/> rows. The relationship to <see cref="Session"/> is configured on the
/// aggregate root (<see cref="SessionConfiguration"/>); here only the scalar columns and the lookup index.
/// </summary>
internal sealed class SessionMaterialConfiguration : IEntityTypeConfiguration<SessionMaterial>
{
    public void Configure(EntityTypeBuilder<SessionMaterial> builder)
    {
        builder.ToTable("session_materials");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.SessionId).IsRequired();
        builder.Property(m => m.FileName).HasMaxLength(260).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();

        // R2 object key only (FR-PLAT-AST-004).
        builder.Property(m => m.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(m => m.SizeBytes).IsRequired();

        builder.HasIndex(m => m.SessionId);
    }
}
