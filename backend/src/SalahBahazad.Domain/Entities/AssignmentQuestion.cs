using SalahBahazad.Domain.Common;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// One question of a <see cref="UserAssignment"/> — an <b>immutable snapshot</b> of a bank question (or one of
/// its variations) taken at generation time (FR-PLAT-ASG-001, FR-PLAT-SES-007). Body, image, mark, hint and the
/// option set are frozen copies; only the student's answer (<see cref="SelectedOptionId"/>/<see cref="AnsweredAtUtc"/>)
/// is mutable, and only until the assignment completes. Owned by the <see cref="UserAssignment"/> root.
/// <see cref="IAuditViaEventOnly"/> so per-answer saves never write a field-diff audit row.
/// </summary>
public sealed class AssignmentQuestion : IAuditViaEventOnly
{
    private readonly List<AssignmentOption> _options = [];

    private AssignmentQuestion() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>The originating bank question (provenance only — never re-read for content).</summary>
    public Guid QuestionId { get; private set; }

    /// <summary>One-based position within the assignment.</summary>
    public int Order { get; private set; }

    public string? BodyLatex { get; private set; }

    /// <summary>R2 object key for the question image; null when the body is LaTeX-only.</summary>
    public string? ImageObjectKey { get; private set; }

    public int Mark { get; private set; }

    /// <summary>Hint URL shown only in assignments (FR-PLAT-QB-005); null when none.</summary>
    public string? HintUrl { get; private set; }

    /// <summary>The student's chosen option; null until answered.</summary>
    public Guid? SelectedOptionId { get; private set; }

    public DateTimeOffset? AnsweredAtUtc { get; private set; }

    public IReadOnlyCollection<AssignmentOption> Options => _options.AsReadOnly();

    public bool IsAnswered => SelectedOptionId is not null;

    /// <summary>True when the student's selected option is the correct one (false until answered correctly).</summary>
    public bool IsCorrect => SelectedOptionId is Guid selected
        && _options.Any(o => o.Id == selected && o.IsCorrect);

    /// <summary>Snapshots one question into an immutable assignment question (copies the options).</summary>
    internal static AssignmentQuestion Snapshot(
        Guid questionId,
        int order,
        string? bodyLatex,
        string? imageObjectKey,
        int mark,
        string? hintUrl,
        IReadOnlyList<QuestionOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
            throw new InvalidOperationException("A snapshotted question must carry its options.");

        var question = new AssignmentQuestion
        {
            QuestionId = questionId,
            Order = order,
            BodyLatex = bodyLatex,
            ImageObjectKey = imageObjectKey,
            Mark = mark,
            HintUrl = hintUrl,
        };
        // Copy each option into a fresh snapshot value so later bank edits never bleed through.
        foreach (var option in options.OrderBy(o => o.Order))
            question._options.Add(AssignmentOption.Copy(option.Order, option.Text, option.IsCorrect));
        return question;
    }

    /// <summary>Records the student's selected option; the option must belong to this snapshotted question.</summary>
    internal void SelectAnswer(Guid optionId, DateTimeOffset now)
    {
        if (_options.All(o => o.Id != optionId))
            throw new InvalidOperationException("The selected option does not belong to this question.");

        SelectedOptionId = optionId;
        AnsweredAtUtc = now;
    }
}

/// <summary>
/// One snapshotted MCQ option of an <see cref="AssignmentQuestion"/> — a frozen copy of a bank option's text,
/// order and correctness, with its own stable id the student selects by. Owned value; immutable once written.
/// <see cref="IAuditViaEventOnly"/> so it never emits an audit row of its own.
/// </summary>
public sealed class AssignmentOption : IAuditViaEventOnly
{
    private AssignmentOption() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>Zero-based display position within the snapshotted option set.</summary>
    public int Order { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    internal static AssignmentOption Copy(int order, string text, bool isCorrect)
        => new() { Order = order, Text = text, IsCorrect = isCorrect };
}
