using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="QuestionVariation"/> rows. The relationship to <see cref="Question"/> is configured on
/// the aggregate root (<see cref="QuestionConfiguration"/>); here the scalar columns and the variation's
/// own owned option set.
/// </summary>
internal sealed class QuestionVariationConfiguration : IEntityTypeConfiguration<QuestionVariation>
{
    public void Configure(EntityTypeBuilder<QuestionVariation> builder)
    {
        builder.ToTable("question_variations");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.QuestionId).IsRequired();
        builder.Property(v => v.BodyLatex).HasMaxLength(4000);
        builder.Property(v => v.ImageObjectKey).HasMaxLength(512);

        builder.OwnsMany(v => v.Options, options =>
        {
            options.ToTable("question_variation_options");
            options.WithOwner().HasForeignKey("QuestionVariationId");
            options.HasKey(o => o.Id);
            options.Property(o => o.Id).ValueGeneratedNever();
            options.Property(o => o.Text).HasMaxLength(2000).IsRequired();
            options.Property(o => o.Order).HasColumnName("DisplayOrder").IsRequired();
            options.Property(o => o.IsCorrect).IsRequired();
        });
        builder.Navigation(v => v.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(v => v.QuestionId);
    }
}
