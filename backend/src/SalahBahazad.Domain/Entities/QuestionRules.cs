namespace SalahBahazad.Domain.Entities;

/// <summary>
/// Shared invariants for a <see cref="Question"/> and its <see cref="QuestionVariation"/>s
/// (FR-PLAT-QB-001/002): a body must be present as LaTeX and/or an image, and an option set must have at
/// least two options with exactly one marked correct. Centralised so both owners enforce them identically.
/// </summary>
internal static class QuestionRules
{
    /// <summary>Requires LaTeX text and/or an image to be present (FR-PLAT-QB-002).</summary>
    public static void RequireBody(string? bodyLatex, string? imageObjectKey)
    {
        if (string.IsNullOrWhiteSpace(bodyLatex) && string.IsNullOrWhiteSpace(imageObjectKey))
            throw new InvalidOperationException(
                "A question requires LaTeX text and/or an image (FR-PLAT-QB-002).");
    }

    /// <summary>Validates and materialises an ordered option set (FR-PLAT-QB-001).</summary>
    public static List<QuestionOption> BuildOptions(IReadOnlyList<QuestionOptionDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        if (drafts.Count < 2)
            throw new InvalidOperationException("A question must have at least two options (FR-PLAT-QB-001).");
        if (drafts.Count(d => d.IsCorrect) != 1)
            throw new InvalidOperationException(
                "A question must have exactly one correct option (FR-PLAT-QB-001).");

        var options = new List<QuestionOption>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
            options.Add(QuestionOption.Create(i, drafts[i].Text, drafts[i].IsCorrect));
        return options;
    }

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
