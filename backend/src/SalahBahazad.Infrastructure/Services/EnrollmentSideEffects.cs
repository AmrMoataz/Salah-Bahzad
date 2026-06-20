using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Real post-enrolment assessment provisioning (<see cref="IEnrollmentSideEffects"/>, FR-PLAT-ENR-005): on
/// <c>EnrollmentCreated</c>/<c>Extended</c> it snapshots the session's question bank into a per-student
/// <see cref="UserAssignment"/> (FR-PLAT-ASG-001) and, when the session has a prerequisite with a gating quiz,
/// a per-student <see cref="UserQuiz"/> sourced from the prerequisite's bank + settings (FR-PLAT-QZ-001/002).
/// Each is <b>idempotent</b> independently — if one already exists for the enrollment it is left untouched, so
/// re-enrol/extend keeps existing progress (FR-PLAT-ENR-003). Runs post-commit in the enrolling request's scope,
/// so the generation audit rows resolve the tenant and are attributed to the System actor (FR-PLAT-AUD-005).
/// </summary>
internal sealed class EnrollmentSideEffects(
    IAppDbContext db, TimeProvider clock, ILogger<EnrollmentSideEffects> logger)
    : IEnrollmentSideEffects
{
    public async Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);
        if (enrollment is null)
        {
            logger.LogWarning(
                "Enrollment {EnrollmentId} not found post-commit; no assessments generated.", enrollmentId);
            return;
        }

        // The enrolment's own session (B) — the assignment's source and the quiz's gated target.
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Id == enrollment.SessionId, cancellationToken);
        if (session is null)
            return;

        await GenerateAssignmentAsync(enrollment, session, cancellationToken);
        await GenerateQuizAsync(enrollment, session, cancellationToken);
    }

    /// <summary>
    /// Snapshots the enrolment session's own bank into the per-student assignment (FR-PLAT-ASG-001). Idempotent
    /// (FR-PLAT-ENR-003): never regenerates over an existing assignment + its saved progress. A session with no
    /// question bank yields no assignment.
    /// </summary>
    private async Task GenerateAssignmentAsync(
        Enrollment enrollment, Session session, CancellationToken cancellationToken)
    {
        var alreadyGenerated = await db.UserAssignments
            .AnyAsync(a => a.EnrollmentId == enrollment.Id, cancellationToken);
        if (alreadyGenerated)
        {
            logger.LogInformation(
                "Assignment already exists for enrollment {EnrollmentId}; generation skipped.", enrollment.Id);
            return;
        }

        // Owned option sets (base + variations) load with their owners; only HasMany variations need Include.
        var questions = await db.Questions
            .Include(q => q.Variations)
            .Where(q => q.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        // A session with no question bank yields no assignment — the prerequisite gate passes it vacuously.
        if (questions.Count == 0)
        {
            logger.LogInformation(
                "Session {SessionId} has no questions; no assignment generated for enrollment {EnrollmentId}.",
                session.Id, enrollment.Id);
            return;
        }

        var assignment = UserAssignment.GenerateFor(
            enrollment.TenantId, enrollment, session, questions, PickAssignmentForm, clock.GetUtcNow());

        db.UserAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Generated assignment {AssignmentId} ({QuestionCount} question(s)) for enrollment {EnrollmentId}.",
            assignment.Id, assignment.QuestionCount, enrollment.Id);
    }

    /// <summary>
    /// Generates the gating quiz from the <b>prerequisite</b> (FR-PLAT-QZ-001): iff the enrolment session B has a
    /// prerequisite A, A has a <see cref="QuizSetting"/>, and A has quiz-eligible questions, snapshots A's
    /// settings into one <see cref="UserQuiz"/> for the enrollment. Idempotent (one quiz per enrollment). No
    /// prerequisite, no settings, or no eligible questions ⇒ no quiz (B's videos aren't quiz-gated).
    /// </summary>
    private async Task GenerateQuizAsync(
        Enrollment enrollment, Session gatedSession, CancellationToken cancellationToken)
    {
        if (gatedSession.PrerequisiteSessionId is not Guid prerequisiteId)
            return; // no prerequisite ⇒ not quiz-gated (FR-PLAT-QZ-001)

        var alreadyGenerated = await db.UserQuizzes
            .AnyAsync(q => q.EnrollmentId == enrollment.Id, cancellationToken);
        if (alreadyGenerated)
        {
            logger.LogInformation(
                "Quiz already exists for enrollment {EnrollmentId}; generation skipped.", enrollment.Id);
            return;
        }

        // The prerequisite (A) and its owned 1:1 QuizSetting (loaded with the owner).
        var source = await db.Sessions
            .FirstOrDefaultAsync(s => s.Id == prerequisiteId, cancellationToken);
        if (source?.QuizSetting is null)
        {
            logger.LogInformation(
                "Prerequisite {PrerequisiteId} has no quiz settings; no quiz generated for enrollment {EnrollmentId}.",
                prerequisiteId, enrollment.Id);
            return;
        }

        // A's quiz-eligible bank (FR-PLAT-QB-004). No eligible questions ⇒ no quiz.
        var eligibleCount = await db.Questions
            .CountAsync(q => q.SessionId == prerequisiteId && q.IsValidForQuiz, cancellationToken);
        if (eligibleCount == 0)
        {
            logger.LogInformation(
                "Prerequisite {PrerequisiteId} has no quiz-eligible questions; no quiz generated for enrollment {EnrollmentId}.",
                prerequisiteId, enrollment.Id);
            return;
        }

        var quiz = UserQuiz.GenerateFor(
            enrollment.TenantId, enrollment, gatedSession, source, source.QuizSetting, clock.GetUtcNow());

        db.UserQuizzes.Add(quiz);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Generated quiz {QuizId} from prerequisite {PrerequisiteId} for enrollment {EnrollmentId}.",
            quiz.Id, prerequisiteId, enrollment.Id);
    }

    /// <summary>
    /// Variation-pick strategy for assignments (backend-owned, FR-PLAT-ASG-001/FR-PLAT-QB-003): when a question
    /// has variations, pick uniformly between the base form and each variation; otherwise use the base. The
    /// chosen form's options are copied into the immutable snapshot by the domain.
    /// </summary>
    private static AssignmentQuestionForm PickAssignmentForm(Question question)
    {
        var variations = question.Variations;
        if (variations.Count > 0)
        {
            var pick = Random.Shared.Next(variations.Count + 1); // 0 = base form, 1..n = variations
            if (pick > 0)
            {
                var variation = variations.ElementAt(pick - 1);
                return new AssignmentQuestionForm(
                    variation.BodyLatex, variation.ImageObjectKey, [.. variation.Options]);
            }
        }
        return new AssignmentQuestionForm(question.BodyLatex, question.ImageObjectKey, [.. question.Options]);
    }
}
