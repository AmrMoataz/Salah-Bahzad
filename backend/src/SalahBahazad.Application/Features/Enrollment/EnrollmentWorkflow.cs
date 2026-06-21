using Microsoft.EntityFrameworkCore;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Domain.Entities;
using SalahBahazad.Domain.Enums;
using AttendanceEntity = SalahBahazad.Domain.Entities.Attendance;
using EnrollmentEntity = SalahBahazad.Domain.Entities.Enrollment;
using SessionEntity = SalahBahazad.Domain.Entities.Session;

namespace SalahBahazad.Application.Features.Enrollment;

/// <summary>
/// The single "cycle of truth" both redeem (#12) and unlock (#9) funnel through (FR-PLAT-ENR-005), so counter
/// provisioning, payment, the attendance shell and the enrollment domain event fire identically. Enforces
/// one active enrollment per student+session (FR-PLAT-ENR-006 → 409) and reuses an existing non-active row in
/// place rather than duplicating it (FR-PLAT-ENR-004). The <paramref name="session"/> must have its videos
/// loaded so per-video access counters can be provisioned.
/// </summary>
internal static class EnrollmentWorkflow
{
    public static async Task<EnrollmentEntity> EnrollOrExtendAsync(
        IAppDbContext db,
        Guid tenantId,
        SessionEntity session,
        Guid studentId,
        string studentName,
		EnrollmentMethod method,
        Guid? codeId,
        decimal amount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await EnforcePrerequisiteGateAsync(db, session, studentId, cancellationToken);

        var existing = await db.Enrollments
            .Include(e => e.VideoAccesses)
            .Where(e => e.StudentId == studentId && e.SessionId == session.Id)
            .OrderByDescending(e => e.EnrolledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { Status: EnrollmentStatus.Active })
            throw new ConflictException(
                "This student already has an active enrollment for this session.");

        EnrollmentEntity enrollment;
        if (existing is not null)
        {
            // Re-activate the existing (refunded/expired) row in place — reset counters, push expiry, new payment.
            existing.Extend(session, method, codeId, amount, now);
            enrollment = existing;
        }
        else
        {
            enrollment = EnrollmentEntity.Create(tenantId, studentId, studentName, session, method, codeId, amount, now);
            db.Enrollments.Add(enrollment);
        }

        await EnsureAttendanceShellAsync(db, tenantId, studentId, session.Id, enrollment.Id, cancellationToken);
        return enrollment;
    }

    /// <summary>
    /// Enforces the prerequisite-assignment gate (FR-PLAT-ENR-007): if the session has a prerequisite, the
    /// student must hold a <see cref="AssignmentStatus.Completed"/> assignment for it before enrolling (→409).
    /// When the prerequisite has <b>no</b> question bank there is nothing to complete, so the gate passes
    /// vacuously — closing the Phase-4 deferral.
    /// </summary>
    private static async Task EnforcePrerequisiteGateAsync(
        IAppDbContext db, SessionEntity session, Guid studentId, CancellationToken cancellationToken)
    {
        if (session.PrerequisiteSessionId is not Guid prerequisiteId)
            return;

        var prerequisiteHasQuestions = await db.Questions
            .AnyAsync(q => q.SessionId == prerequisiteId, cancellationToken);
        if (!prerequisiteHasQuestions)
            return; // vacuous pass — no assignment to complete.

        var completed = await db.UserAssignments.AnyAsync(
            a => a.StudentId == studentId
                 && a.SessionId == prerequisiteId
                 && a.Status == AssignmentStatus.Completed,
            cancellationToken);

        if (!completed)
            throw new ConflictException("Complete the prerequisite assignment first.");
    }

    /// <summary>Creates the per-(student, session) attendance shell once (FR-PLAT-ATT-001); idempotent on re-enroll.</summary>
    private static async Task EnsureAttendanceShellAsync(
        IAppDbContext db, Guid tenantId, Guid studentId, Guid sessionId, Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Attendances
            .AnyAsync(a => a.StudentId == studentId && a.SessionId == sessionId, cancellationToken);

        if (!exists)
            db.Attendances.Add(AttendanceEntity.CreateShell(tenantId, studentId, sessionId, enrollmentId));
    }
}
