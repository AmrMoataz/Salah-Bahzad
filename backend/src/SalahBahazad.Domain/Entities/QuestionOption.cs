namespace SalahBahazad.Domain.Entities;

/// <summary>
/// One MCQ answer option, owned by a <see cref="Question"/> or a <see cref="QuestionVariation"/>
/// (FR-PLAT-QB-001). An owned value with a stable id and an explicit <see cref="Order"/> so the editor's
/// A/B/C/D ordering round-trips. The option set is replaced wholesale via the owner's mutators, which
/// enforce the "≥ 2 options, exactly one correct" invariant.
/// </summary>
public sealed class QuestionOption
{
    private QuestionOption() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>Zero-based display position within its option set.</summary>
    public int Order { get; private set; }

    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    internal static QuestionOption Create(int order, string text, bool isCorrect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new QuestionOption { Order = order, Text = text.Trim(), IsCorrect = isCorrect };
    }
}

/// <summary>
/// Input shape for (re)building an option set — carried from the Application command into the domain
/// mutators. Identity is server-assigned in Phase 3 (the mutable bank has no answer references yet), so
/// only the text and correctness are supplied.
/// </summary>
public sealed record QuestionOptionDraft(string Text, bool IsCorrect);
