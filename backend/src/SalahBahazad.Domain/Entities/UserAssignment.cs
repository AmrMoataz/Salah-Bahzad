using SalahBahazad.Domain.Common;
using SalahBahazad.Domain.Enums;
using SalahBahazad.Domain.Events;

namespace SalahBahazad.Domain.Entities;

/// <summary>
/// A student's open-book assignment for a session (FR-PLAT-ASG-001..006): an <b>immutable snapshot</b> of the
/// question bank taken at generation time, so later edits to the bank never alter an issued assignment
/// (snapshot fairness, FR-PLAT-SES-007). Generated once per enrolment by the side-effect seam and retained
/// past expiry (FR-PLAT-ENR-003); re-enrol reuses it. Tenant-scoped. <see cref="IAuditViaEventOnly"/>: only its
/// lifecycle events (<see cref="AssignmentGeneratedEvent"/>/<see cref="AssignmentGradedEvent"/>) leave an audit
/// row — the per-answer saves do not (their behaviour goes to <see cref="AssessmentEvent"/>).
/// </summary>
public sealed class UserAssignment : TenantEntityBase, IAuditViaEventOnly
{
    private readonly List<AssignmentQuestion> _questions = [];

    private UserAssignment() { }

    public Guid EnrollmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid SessionId { get; private set; }

    public AssignmentStatus Status { get; private set; } = AssignmentStatus.InProgress;

    /// <summary>Total marks scored; null until the assignment is <see cref="AssignmentStatus.Completed"/>.</summary>
    public int? ScoreMarks { get; private set; }

    /// <summary>Sum of the snapshotted question marks (the denominator for the percent).</summary>
    public int MaxMarks { get; private set; }

    /// <summary>Count of correctly-answered questions; null until completed.</summary>
    public int? CorrectCount { get; private set; }

    public int QuestionCount { get; private set; }

    /// <summary>Accumulated time spent across sittings (FR-PLAT-ASG-004).</summary>
    public int TimeSpentSeconds { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<AssignmentQuestion> Questions => _questions.AsReadOnly();

    /// <summary>Number of questions the student has answered so far.</summary>
    public int AnsweredCount => _questions.Count(q => q.IsAnswered);

    /// <summary>Score as a 0–100 percent (rounded); 0 when there are no marks. Computed from current answers.</summary>
    public int Percent => MaxMarks == 0
        ? 0
        : (int)Math.Round(CurrentScoreMarks * 100.0 / MaxMarks, MidpointRounding.AwayFromZero);

    private int CurrentScoreMarks => _questions.Where(q => q.IsCorrect).Sum(q => q.Mark);

    /// <summary>
    /// Generates the immutable snapshot for an enrolment (FR-PLAT-ASG-001). Each question contributes exactly
    /// one snapshotted form chosen by <paramref name="pickForm"/> (the base question or a random variation), its
    /// options <b>copied</b> so the assignment is independent of later bank edits (FR-PLAT-SES-007).
    /// <paramref name="questions"/> must be non-empty — a session with no question bank yields no assignment.
    /// </summary>
    public static UserAssignment GenerateFor(
        Guid tenantId,
        Enrollment enrollment,
        Session session,
        IReadOnlyList<Question> questions,
        Func<Question, AssignmentQuestionForm> pickForm,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(pickForm);
        if (questions.Count == 0)
            throw new InvalidOperationException("Cannot generate an assignment with no questions.");

        var assignment = new UserAssignment
        {
            EnrollmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            SessionId = session.Id,
            Status = AssignmentStatus.InProgress,
            QuestionCount = questions.Count,
            TimeSpentSeconds = 0,
            StartedAtUtc = now,
        };
        assignment.SetTenant(tenantId);

        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            var form = pickForm(question);
            assignment._questions.Add(AssignmentQuestion.Snapshot(
                questionId: question.Id,
                order: i + 1, // 1-based question position (contract §C)
                bodyLatex: form.BodyLatex,
                imageObjectKey: form.ImageObjectKey,
                mark: question.Mark,
                hintUrl: question.HintUrl,
                options: form.Options));
        }

        assignment.MaxMarks = assignment._questions.Sum(q => q.Mark);
        assignment.AddDomainEvent(new AssignmentGeneratedEvent(
            assignment.Id, assignment.StudentId, assignment.SessionId, assignment.QuestionCount));
        return assignment;
    }

    /// <summary>
    /// Records the student's answer to one snapshotted question (FR-PLAT-ASG-003). Re-answering is allowed while
    /// <see cref="AssignmentStatus.InProgress"/> (open-access); once every question is answered the assignment is
    /// auto-graded and sealed (FR-PLAT-ASG-006) — a later answer is rejected. Returns the answered question's
    /// 1-based order (for the behaviour log).
    /// </summary>
    public int Answer(Guid assignmentQuestionId, Guid selectedOptionId, DateTimeOffset now)
    {
        if (Status == AssignmentStatus.Completed)
            throw new InvalidOperationException("This assignment is already completed and cannot be changed.");

        var question = _questions.FirstOrDefault(q => q.Id == assignmentQuestionId)
            ?? throw new InvalidOperationException(
                $"Question '{assignmentQuestionId}' is not part of this assignment.");

        question.SelectAnswer(selectedOptionId, now);

        if (_questions.All(q => q.IsAnswered))
            Grade(now);

        return question.Order;
    }

    /// <summary>Accrues think/solve time across sittings (FR-PLAT-ASG-004). Ignores non-positive deltas.</summary>
    public void AddTime(int seconds)
    {
        if (seconds > 0)
            TimeSpentSeconds += seconds;
    }

    private void Grade(DateTimeOffset now)
    {
        ScoreMarks = CurrentScoreMarks;
        CorrectCount = _questions.Count(q => q.IsCorrect);
        Status = AssignmentStatus.Completed;
        CompletedAtUtc = now;
        AddDomainEvent(new AssignmentGradedEvent(
            Id, StudentId, SessionId, Percent, ScoreMarks.Value, MaxMarks));
    }
}

/// <summary>
/// The renderable form chosen for one question when snapshotting an assignment — the base question or one of its
/// variations (FR-PLAT-QB-003). Carries only what differs between forms (body/image/options); the question's
/// mark and hint are taken from the <see cref="Question"/> itself. The variation-pick strategy is the
/// backend's; the options are copied into the snapshot, never shared.
/// </summary>
public sealed record AssignmentQuestionForm(
    string? BodyLatex, string? ImageObjectKey, IReadOnlyList<QuestionOption> Options);
