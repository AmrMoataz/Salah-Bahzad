using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SessionVideo"/> rows. The relationship to <see cref="Session"/> is configured on the
/// aggregate root (<see cref="SessionConfiguration"/>); here only the scalar columns and the ordered
/// lookup index are defined.
/// </summary>
internal sealed class SessionVideoConfiguration : IEntityTypeConfiguration<SessionVideo>
{
    public void Configure(EntityTypeBuilder<SessionVideo> builder)
    {
        builder.ToTable("session_videos");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.SessionId).IsRequired();
        builder.Property(v => v.Title).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Order).IsRequired();
        builder.Property(v => v.LengthMinutes).IsRequired();
        builder.Property(v => v.AccessCount).IsRequired();

        // R2 keys only (FR-PLAT-VID-007).
        builder.Property(v => v.SourceObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(v => v.HlsManifestKey).HasMaxLength(512);

        builder.Property(v => v.ProcessingStatus).IsRequired();

        builder.HasIndex(v => new { v.SessionId, v.Order });
    }
}
