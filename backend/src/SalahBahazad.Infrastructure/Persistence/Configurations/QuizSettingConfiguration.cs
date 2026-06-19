using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="QuizSetting"/> value owned 1:1 by a <see cref="Session"/> (FR-PLAT-SES-006).
/// Configured through the owner (owned types cannot be standalone <c>IEntityTypeConfiguration</c>s);
/// columns are left nullable so an unset quiz (the whole owned reference being null) is representable.
/// </summary>
internal static class QuizSettingConfiguration
{
    public static void Configure(OwnedNavigationBuilder<Session, QuizSetting> builder)
    {
        builder.Property(q => q.TimeLimitMinutes).HasColumnName("QuizTimeLimitMinutes");
        builder.Property(q => q.QuestionCount).HasColumnName("QuizQuestionCount");
        builder.Property(q => q.AttemptCount).HasColumnName("QuizAttemptCount");
        builder.Property(q => q.MinPassPercent).HasColumnName("QuizMinPassPercent");
    }
}
