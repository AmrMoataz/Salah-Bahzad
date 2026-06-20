using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;

namespace SalahBahazad.Infrastructure.Services;

/// <summary>
/// Real post-enrolment assessment provisioning (<see cref="IEnrollmentSideEffects"/>, FR-PLAT-ENR-005): on
/// <c>EnrollmentCreated</c>/<c>Extended</c> it snapshots the session's question bank into a per-student
/// <see cref="UserAssignment"/> (FR-PLAT-ASG-001). <b>Idempotent</b> — if an assignment already exists for the
/// enrollment it is left untouched, so re-enrol/extend keeps the existing assignment and progress
/// (FR-PLAT-ENR-003). The prerequisite-<i>quiz</i> snapshot remains a logged no-op until 5B-2. Runs post-commit
/// in the enrolling request's scope, so the generation's audit row resolves the tenant and is attributed to the
/// System actor (FR-PLAT-AUD-005).
/// </summary>
internal sealed class EnrollmentSideEffects(
    IAppDbContext db, TimeProvider clock, ILogger<EnrollmentSideEffects> logger)
    : IEnrollmentSideEffects
{
    public async Task GenerateAssessmentsAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        // Idempotent (FR-PLAT-ENR-003): never regenerate over an existing assignment + its saved progress.
        var alreadyGenerated = await db.UserAssignments
            .AnyAsync(a => a.EnrollmentId == enrollmentId, cancellationToken);
        if (alreadyGenerated)
        {
            logger.LogInformation(
                "Assignment already exists for enrollment {EnrollmentId}; generation skipped.", enrollmentId);
            return;
        }

        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);
        if (enrollment is null)
        {
            logger.LogWarning(
                "Enrollment {EnrollmentId} not found post-commit; no assignment generated.", enrollmentId);
            return;
        }

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Id == enrollment.SessionId, cancellationToken);
        if (session is null)
            return;

        // Owned option sets (base + variations) load with their owners; only HasMany variations need Include.
        var questions = await db.Questions
            .Include(q => q.Variations)
            .Where(q => q.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        // A session with no question bank yields no assignment — the prerequisite gate passes it vacuously.
        // (Prerequisite-quiz snapshot generation, FR-PLAT-QZ-001, stays a no-op until 5B-2.)
        if (questions.Count == 0)
        {
            logger.LogInformation(
                "Session {SessionId} has no questions; no assignment generated for enrollment {EnrollmentId}.",
                session.Id, enrollmentId);
            return;
        }

        var assignment = UserAssignment.GenerateFor(
            enrollment.TenantId, enrollment, session, questions, PickForm, clock.GetUtcNow());

        db.UserAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Generated assignment {AssignmentId} ({QuestionCount} question(s)) for enrollment {EnrollmentId}.",
            assignment.Id, assignment.QuestionCount, enrollmentId);
    }

    /// <summary>
    /// Variation-pick strategy (backend-owned, FR-PLAT-ASG-001/FR-PLAT-QB-003): when a question has variations,
    /// pick uniformly between the base form and each variation; otherwise use the base. The chosen form's
    /// options are copied into the immutable snapshot by the domain.
    /// </summary>
    private static AssignmentQuestionForm PickForm(Question question)
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
